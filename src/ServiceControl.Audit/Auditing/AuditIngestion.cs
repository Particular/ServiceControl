namespace ServiceControl.Audit.Auditing
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
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Raw;
    using NServiceBus.Transport;
    using Raven.Client;
    using ServiceControl.Infrastructure.Metrics;

    class AuditIngestion : IHostedService
    {
        static readonly long FrequencyInMilliseconds = Stopwatch.Frequency / 1000;

        public AuditIngestion(
            Settings settings,
            RawEndpointFactory rawEndpointFactory,
            Metrics metrics,
            IDocumentStore failedImportsStorage,
            LoggingSettings loggingSettings,
            AuditIngestionCustomCheck.State ingestionState,
            AuditIngestionComponent auditIngestionComponent)
        {
            inputEndpoint = settings.AuditQueue;
            this.rawEndpointFactory = rawEndpointFactory;
            auditIngestor = auditIngestionComponent.Ingestor;
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

            watchdog = new Watchdog(EnsureStarted, EnsureStopped, ingestionState.ReportError,
                ingestionState.Clear, settings.TimeToRestartAuditIngestionAfterFailure, logger, "audit message ingestion");

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

        Task OnCriticalError(string failure, Exception arg2)
        {
            logger.Warn($"OnCriticalError. '{failure}'", arg2);
            return watchdog.OnFailure(failure);
        }

        Task OnCriticalErrorAction(ICriticalErrorContext ctx) => OnCriticalError(ctx.Error, ctx.Exception);

        async Task EnsureStarted(CancellationToken cancellationToken = default)
        {
            try
            {
                logger.Debug("Ensure started. Start/stop semaphore acquiring");
                await startStopSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                logger.Debug("Ensure started. Start/stop semaphore acquired");

                if (ingestionEndpoint != null)
                {
                    logger.Debug("Ensure started. Already started, skipping start up");
                    return; //Already started
                }

                var rawConfiguration = rawEndpointFactory.CreateAuditIngestor(inputEndpoint, OnMessage);

                rawConfiguration.Settings.Set("onCriticalErrorAction", (Func<ICriticalErrorContext, Task>)OnCriticalErrorAction);

                rawConfiguration.CustomErrorHandlingPolicy(errorHandlingPolicy);

                logger.Info("Ensure started. Infrastructure starting");
                var startableRaw = await RawEndpoint.Create(rawConfiguration).ConfigureAwait(false);

                await auditIngestor.VerifyCanReachForwardingAddress(startableRaw).ConfigureAwait(false);

                dispatcher = startableRaw;
                ingestionEndpoint = await startableRaw.Start()
                    .ConfigureAwait(false);
                logger.Info("Ensure started. Infrastructure started");
            }
            finally
            {
                logger.Debug("Ensure started. Start/stop semaphore releasing");
                startStopSemaphore.Release();
                logger.Debug("Ensure started. Start/stop semaphore released");
            }
        }

        async Task EnsureStopped(CancellationToken cancellationToken = default)
        {
            try
            {
                logger.Info("Shutting down. Start/stop semaphore acquiring");
                await startStopSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                logger.Info("Shutting down. Start/stop semaphore acquired");

                if (ingestionEndpoint == null)
                {
                    logger.Info("Shutting down. Already stopped, skipping shut down");
                    return; //Already stopped
                }
                var stoppable = ingestionEndpoint;
                ingestionEndpoint = null;
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

        async Task OnMessage(MessageContext messageContext, IDispatchMessages dispatcher)
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

        IReceivingRawEndpoint ingestionEndpoint;
        IDispatchMessages dispatcher;

        readonly SemaphoreSlim startStopSemaphore = new SemaphoreSlim(1);
        readonly string inputEndpoint;
        readonly RawEndpointFactory rawEndpointFactory;
        readonly IErrorHandlingPolicy errorHandlingPolicy;
        readonly AuditIngestor auditIngestor;
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