﻿namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Infrastructure.Settings;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using Persistence;
    using Persistence.UnitOfWork;
    using ServiceControl.Infrastructure;
    using Transports;

    class AuditIngestion : BackgroundService
    {
        public AuditIngestion(
            Settings settings,
            ITransportCustomization transportCustomization,
            TransportSettings transportSettings,
            IFailedAuditStorage failedImportsStorage,
            AuditIngestionCustomCheck.State ingestionState,
            AuditIngestor auditIngestor,
            IAuditIngestionUnitOfWorkFactory unitOfWorkFactory,
            IHostApplicationLifetime applicationLifetime)
        {
            inputEndpoint = settings.AuditQueue;
            this.transportCustomization = transportCustomization;
            this.transportSettings = transportSettings;
            this.auditIngestor = auditIngestor;
            this.unitOfWorkFactory = unitOfWorkFactory;
            this.settings = settings;
            this.applicationLifetime = applicationLifetime;

            if (!transportSettings.MaxConcurrency.HasValue)
            {
                throw new ArgumentException("MaxConcurrency is not set in TransportSettings");
            }

            channel = Channel.CreateBounded<MessageContext>(new BoundedChannelOptions(transportSettings.MaxConcurrency.Value)
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });

            errorHandlingPolicy = new AuditIngestionFaultPolicy(failedImportsStorage, settings.LoggingSettings, OnCriticalError);

            watchdog = new Watchdog(
                "audit message ingestion",
                EnsureStarted,
                EnsureStopped,
                ingestionState.ReportError,
                ingestionState.Clear,
                settings.TimeToRestartAuditIngestionAfterFailure,
                logger
            );
        }

        Task OnCriticalError(string failure, Exception exception)
        {
            logger.Fatal($"OnCriticalError. '{failure}'", exception);
            return watchdog.OnFailure(failure);
        }

        async Task EnsureStarted(CancellationToken cancellationToken)
        {
            try
            {
                await startStopSemaphore.WaitAsync(cancellationToken);

                var canIngest = unitOfWorkFactory.CanIngestMore();

                logger.DebugFormat("Ensure started {0}", canIngest);

                if (canIngest)
                {
                    await SetUpAndStartInfrastructure(cancellationToken);
                }
                else
                {
                    await StopAndTeardownInfrastructure(cancellationToken);
                }
            }
            catch (Exception e)
            {
                try
                {
                    await StopAndTeardownInfrastructure(cancellationToken);
                }
                catch (Exception teardownException)
                {
                    throw new AggregateException(e, teardownException);
                }

                throw;
            }
            finally
            {
                startStopSemaphore.Release();
            }
        }

        async Task SetUpAndStartInfrastructure(CancellationToken cancellationToken)
        {
            if (messageReceiver != null)
            {
                logger.Debug("Infrastructure already Started");
                return;
            }

            try
            {
                logger.Info("Starting infrastructure");
                transportInfrastructure = await transportCustomization.CreateTransportInfrastructure(
                    inputEndpoint,
                    transportSettings,
                    OnMessage,
                    errorHandlingPolicy.OnError,
                    OnCriticalError,
                    TransportTransactionMode.ReceiveOnly
                );

                messageReceiver = transportInfrastructure.Receivers[inputEndpoint];

                await auditIngestor.VerifyCanReachForwardingAddress();
                await messageReceiver.StartReceive(cancellationToken);

                logger.Info(LogMessages.StartedInfrastructure);
            }
            catch (Exception e)
            {
                logger.Error("Failed to start infrastructure", e);
                throw;
            }
        }

        async Task StopAndTeardownInfrastructure(CancellationToken cancellationToken)
        {
            if (transportInfrastructure == null)
            {
                logger.Debug("Infrastructure already Stopped");
                return;
            }

            try
            {
                logger.Info("Stopping infrastructure");
                try
                {
                    if (messageReceiver != null)
                    {
                        await messageReceiver.StopReceive(cancellationToken);
                    }
                }
                finally
                {
                    await transportInfrastructure.Shutdown(cancellationToken);
                }

                messageReceiver = null;
                transportInfrastructure = null;

                logger.Info(LogMessages.StoppedInfrastructure);
            }
            catch (Exception e)
            {
                logger.Error("Failed to stop infrastructure", e);
                throw;
            }
        }

        async Task EnsureStopped(CancellationToken cancellationToken)
        {
            try
            {
                await startStopSemaphore.WaitAsync(cancellationToken);
                await StopAndTeardownInfrastructure(cancellationToken);
            }
            finally
            {
                startStopSemaphore.Release();
            }
        }

        async Task OnMessage(MessageContext messageContext, CancellationToken cancellationToken)
        {
            var tags = Telemetry.GetIngestedMessageTags(messageContext.Headers, messageContext.Body);
            using (new DurationRecorder(ingestionDuration, tags))
            {
                if (settings.MessageFilter != null && settings.MessageFilter(messageContext))
                {
                    return;
                }

                var taskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                messageContext.SetTaskCompletionSource(taskCompletionSource);

                await channel.Writer.WriteAsync(messageContext, cancellationToken);
                await taskCompletionSource.Task;

                successfulMessagesCounter.Add(1, tags);
            }
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await watchdog.Start(() => applicationLifetime.StopApplication(), cancellationToken);
            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                var contexts = new List<MessageContext>(transportSettings.MaxConcurrency.Value);

                while (await channel.Reader.WaitToReadAsync(stoppingToken))
                {
                    // will only enter here if there is something to read.
                    try
                    {
                        // as long as there is something to read this will fetch up to MaximumConcurrency items
                        using (var recorder = new DurationRecorder(batchDuration))
                        {
                            while (channel.Reader.TryRead(out var context))
                            {
                                contexts.Add(context);
                            }

                            recorder.Tags.Add("ingestion.batch_size", contexts.Count);

                            await auditIngestor.Ingest(contexts);
                        }

                        consecutiveBatchFailuresCounter.Record(0);
                    }
                    catch (Exception e)
                    {
                        // signal all message handling tasks to terminate
                        foreach (var context in contexts)
                        {
                            _ = context.GetTaskCompletionSource().TrySetException(e);
                        }

                        if (e is OperationCanceledException && stoppingToken.IsCancellationRequested)
                        {
                            logger.Info("Batch cancelled", e);
                            break;
                        }

                        logger.Info("Ingesting messages failed", e);

                        // no need to do interlocked increment since this is running sequential
                        consecutiveBatchFailuresCounter.Record(consecutiveBatchFailures++);
                    }
                    finally
                    {
                        contexts.Clear();
                    }
                }
                // will fall out here when writer is completed
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // ExecuteAsync cancelled
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await watchdog.Stop(cancellationToken);
                channel.Writer.Complete();
                await base.StopAsync(cancellationToken);
            }
            finally
            {
                if (transportInfrastructure != null)
                {
                    try
                    {
                        await transportInfrastructure.Shutdown(cancellationToken);
                    }
                    catch (OperationCanceledException e) when (cancellationToken.IsCancellationRequested)
                    {
                        logger.Info("Shutdown cancelled", e);
                    }
                }
            }
        }

        TransportInfrastructure transportInfrastructure;
        IMessageReceiver messageReceiver;
        long consecutiveBatchFailures = 0;

        readonly SemaphoreSlim startStopSemaphore = new(1);
        readonly string inputEndpoint;
        readonly ITransportCustomization transportCustomization;
        readonly TransportSettings transportSettings;
        readonly AuditIngestor auditIngestor;
        readonly AuditIngestionFaultPolicy errorHandlingPolicy;
        readonly IAuditIngestionUnitOfWorkFactory unitOfWorkFactory;
        readonly Settings settings;
        readonly Channel<MessageContext> channel;
        readonly Histogram<double> batchDuration = Telemetry.Meter.CreateHistogram<double>(Telemetry.CreateInstrumentName("ingestion", "batch_duration"), unit: "ms", "Average audit message batch processing duration");
        readonly Counter<long> successfulMessagesCounter = Telemetry.Meter.CreateCounter<long>(Telemetry.CreateInstrumentName("ingestion", "success"), description: "Successful ingested audit message count");
        readonly Histogram<long> consecutiveBatchFailuresCounter = Telemetry.Meter.CreateHistogram<long>(Telemetry.CreateInstrumentName("ingestion", "consecutive_batch_failures"), unit: "count", description: "Consecutive audit ingestion batch failure");
        readonly Histogram<double> ingestionDuration = Telemetry.Meter.CreateHistogram<double>(Telemetry.CreateInstrumentName("ingestion", "duration"), unit: "ms", description: "Average incoming audit message processing duration");
        readonly Watchdog watchdog;
        readonly IHostApplicationLifetime applicationLifetime;

        static readonly ILog logger = LogManager.GetLogger<AuditIngestion>();

        internal static class LogMessages
        {
            internal const string StartedInfrastructure = "Started infrastructure";
            internal const string StoppedInfrastructure = "Stopped infrastructure";
        }
    }
}