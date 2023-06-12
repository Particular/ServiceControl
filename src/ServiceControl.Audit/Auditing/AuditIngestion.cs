﻿namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.Settings;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using Persistence;
    using Persistence.UnitOfWork;
    using ServiceControl.Infrastructure.Metrics;
    using ServiceControl.Transports;

    class AuditIngestion : IHostedService
    {
        static readonly long FrequencyInMilliseconds = Stopwatch.Frequency / 1000;

        public AuditIngestion(
            Settings settings,
            TransportCustomization transportCustomization,
            TransportSettings transportSettings,
            Metrics metrics,
            IFailedAuditStorage failedImportsStorage,
            LoggingSettings loggingSettings,
            AuditIngestionCustomCheck.State ingestionState,
            AuditIngestor auditIngestor,
            IAuditIngestionUnitOfWorkFactory unitOfWorkFactory,
            IHostApplicationLifetime applicationLifetime
            )
        {
            inputEndpoint = settings.AuditQueue;
            this.transportCustomization = transportCustomization;
            this.transportSettings = transportSettings;
            this.auditIngestor = auditIngestor;
            this.unitOfWorkFactory = unitOfWorkFactory;
            this.applicationLifetime = applicationLifetime;
            this.settings = settings;

            batchSizeMeter = metrics.GetMeter("Audit ingestion - batch size");
            batchDurationMeter = metrics.GetMeter("Audit ingestion - batch processing duration", FrequencyInMilliseconds);
            receivedMeter = metrics.GetCounter("Audit ingestion - received");

            channel = Channel.CreateBounded<MessageContext>(new BoundedChannelOptions(settings.MaximumConcurrencyLevel)
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });

            errorHandlingPolicy = new AuditIngestionFaultPolicy(failedImportsStorage, loggingSettings, FailedMessageFactory, OnCriticalError);

            watchdog = new Watchdog(
                EnsureStarted,
                EnsureStopped,
                ingestionState.ReportError,
                ingestionState.Clear,
                settings.TimeToRestartAuditIngestionAfterFailure,
                logger,
                "audit message ingestion"
                );

            ingestionWorker = Task.Run(() => Loop(), CancellationToken.None);
        }

        public Task StartAsync(CancellationToken _) => watchdog.Start();

        public async Task StopAsync(CancellationToken _)
        {
            await watchdog.Stop().ConfigureAwait(false);
            channel.Writer.Complete();
            await ingestionWorker.ConfigureAwait(false);
        }

        FailedAuditImport FailedMessageFactory(FailedTransportMessage msg)
        {
            return new FailedAuditImport
            {
                Message = msg
            };
        }

        Task OnCriticalError(string failure, Exception exception)
        {
            logger.Warn($"OnCriticalError. '{failure}'", exception);
            return watchdog.OnFailure(failure);
        }

        async Task EnsureStarted(CancellationToken cancellationToken = default)
        {
            bool failedToStart = false;
            try
            {
                logger.Debug("Ensure started. Start/stop semaphore acquiring");
                await startStopSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                logger.Debug("Ensure started. Start/stop semaphore acquired");

                if (!unitOfWorkFactory.CanIngestMore())
                {
                    if (queueIngestor != null)
                    {
                        var stoppable = queueIngestor;
                        queueIngestor = null;
                        logger.Info("Shutting down due to failed persistence health check. Infrastructure shut down commencing");
                        await stoppable.Stop().ConfigureAwait(false);
                        logger.Info("Shutting down due to failed persistence health check. Infrastructure shut down completed");
                    }
                    return;
                }


                if (queueIngestor != null)
                {
                    logger.Debug("Ensure started. Already started, skipping start up");
                    return; //Already started
                }

                logger.Info("Ensure started. Infrastructure starting");

                queueIngestor = await transportCustomization.InitializeQueueIngestor(
                    inputEndpoint,
                    transportSettings,
                    OnMessage,
                    errorHandlingPolicy.OnError,
                    OnCriticalError).ConfigureAwait(false);

                dispatcher = await transportCustomization.InitializeDispatcher(inputEndpoint, transportSettings).ConfigureAwait(false);

                await auditIngestor.VerifyCanReachForwardingAddress(dispatcher).ConfigureAwait(false);

                await queueIngestor.Start()
                    .ConfigureAwait(false);

                logger.Info("Ensure started. Infrastructure started");
            }
            catch (Exception ex)
            {
                logger.Debug("Failed to start", ex);
                failedToStart = true;
            }
            finally
            {
                logger.Debug("Ensure started. Start/stop semaphore releasing");
                startStopSemaphore.Release();
                logger.Debug("Ensure started. Start/stop semaphore released");

                if (failedToStart)
                {
                    applicationLifetime.StopApplication();
                }
            }
        }

        async Task EnsureStopped(CancellationToken cancellationToken = default)
        {
            try
            {
                logger.Info("Shutting down. Start/stop semaphore acquiring");
                await startStopSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                logger.Info("Shutting down. Start/stop semaphore acquired");

                if (queueIngestor == null)
                {
                    logger.Info("Shutting down. Already stopped, skipping shut down");
                    return; //Already stopped
                }
                var stoppable = queueIngestor;
                queueIngestor = null;
                logger.Info("Shutting down. Infrastructure shut down commencing");
                await stoppable.Stop().ConfigureAwait(false);
                logger.Info("Shutting down. Infrastructure shut down completed");
            }
            finally
            {
                logger.Info("Shutting down. Start/stop semaphore releasing");
                startStopSemaphore.Release();
                logger.Info("Shutting down. Start/stop semaphore released");
            }
        }

        async Task OnMessage(MessageContext messageContext)
        {
            if (settings.MessageFilter != null && settings.MessageFilter(messageContext))
            {
                return;
            }

            var taskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            messageContext.SetTaskCompletionSource(taskCompletionSource);

            receivedMeter.Mark();

            await channel.Writer.WriteAsync(messageContext).ConfigureAwait(false);
            await taskCompletionSource.Task.ConfigureAwait(false);
        }

        async Task Loop()
        {
            var contexts = new List<MessageContext>(settings.MaximumConcurrencyLevel);

            while (await channel.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                // will only enter here if there is something to read.
                try
                {
                    // as long as there is something to read this will fetch up to MaximumConcurrency items
                    while (channel.Reader.TryRead(out var context))
                    {
                        contexts.Add(context);
                    }

                    batchSizeMeter.Mark(contexts.Count);
                    using (batchDurationMeter.Measure())
                    {
                        await auditIngestor.Ingest(contexts, dispatcher).ConfigureAwait(false);
                    }
                }
                catch (Exception e) // show must go on
                {
                    if (logger.IsInfoEnabled)
                    {
                        logger.Info("Ingesting messages failed", e);
                    }

                    // signal all message handling tasks to terminate
                    foreach (var context in contexts)
                    {
                        context.GetTaskCompletionSource().TrySetException(e);
                    }
                }
                finally
                {
                    contexts.Clear();
                }
            }
            // will fall out here when writer is completed
        }

        IQueueIngestor queueIngestor;
        IDispatchMessages dispatcher;

        readonly SemaphoreSlim startStopSemaphore = new SemaphoreSlim(1);
        readonly string inputEndpoint;
        readonly TransportCustomization transportCustomization;
        readonly TransportSettings transportSettings;
        readonly AuditIngestor auditIngestor;
        readonly AuditIngestionFaultPolicy errorHandlingPolicy;
        readonly IAuditIngestionUnitOfWorkFactory unitOfWorkFactory;
        readonly IHostApplicationLifetime applicationLifetime;
        readonly Settings settings;
        readonly Channel<MessageContext> channel;
        readonly Meter batchSizeMeter;
        readonly Meter batchDurationMeter;
        readonly Counter receivedMeter;
        readonly Watchdog watchdog;
        readonly Task ingestionWorker;

        static readonly ILog logger = LogManager.GetLogger<AuditIngestion>();
    }
}