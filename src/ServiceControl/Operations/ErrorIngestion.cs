namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Infrastructure.Metrics;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Logging;
    using NServiceBus.Raw;
    using NServiceBus.Transport;
    using Raven.Client;
    using Recoverability;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Transports;

    class ErrorIngestion : IHostedService
    {
        static ILog log = LogManager.GetLogger<ErrorIngestion>();
        static readonly long FrequencyInMilliseconds = Stopwatch.Frequency / 1000;

        public ErrorIngestion(
            Settings settings,
            TransportCustomization transportCustomization,
            TransportSettings transportSettings,
            RawEndpointFactory rawEndpointFactory,
            Metrics metrics,
            IDocumentStore documentStore,
            LoggingSettings loggingSettings,
            ErrorIngestionCustomCheck.State ingestionState,
            ErrorIngestor ingestor,
            IIngestionUnitOfWorkFactory unitOfWorkFactory)
        {
            this.settings = settings;
            this.transportCustomization = transportCustomization;
            this.transportSettings = transportSettings;
            errorQueue = settings.ErrorQueue;
            this.rawEndpointFactory = rawEndpointFactory;
            this.ingestor = ingestor;
            this.unitOfWorkFactory = unitOfWorkFactory;

            receivedMeter = metrics.GetCounter("Error ingestion - received");
            batchSizeMeter = metrics.GetMeter("Error ingestion - batch size");
            batchDurationMeter = metrics.GetMeter("Error ingestion - batch processing duration", FrequencyInMilliseconds);

            channel = Channel.CreateBounded<MessageContext>(new BoundedChannelOptions(settings.MaximumConcurrencyLevel)
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });

            errorHandlingPolicy = new ErrorIngestionFaultPolicy(documentStore, loggingSettings, (failure, arg2) =>
            {
                log.Warn($"OnCriticalError. '{failure}'", arg2);
                return watchdog.OnFailure(failure);
            });

            watchdog = new Watchdog(EnsureStarted, EnsureStopped, ingestionState.ReportError,
                ingestionState.Clear, settings.TimeToRestartErrorIngestionAfterFailure, log, "failed message ingestion");

            ingestionWorker = Task.Run(() => Loop(), CancellationToken.None);
        }

        public Task StartAsync(CancellationToken _) => watchdog.Start();

        public async Task StopAsync(CancellationToken _)
        {
            await watchdog.Stop().ConfigureAwait(false);
            channel.Writer.Complete();
            await ingestionWorker.ConfigureAwait(false);
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
                        await ingestor.Ingest(contexts, dispatcher).ConfigureAwait(false);
                    }
                }
                catch (Exception e) // show must go on
                {
                    if (log.IsInfoEnabled)
                    {
                        log.Info("Ingesting messages failed", e);
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

        async Task EnsureStarted(CancellationToken cancellationToken = default)
        {
            try
            {
                await startStopSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                if (!unitOfWorkFactory.CanIngestMore())
                {
                    if (queueIngestor != null)
                    {
                        var stoppable = queueIngestor;
                        queueIngestor = null;
                        await stoppable.Stop().ConfigureAwait(false);
                        logger.Info("Shutting down due to failed persistence health check. Infrastructure shut down completed");
                    }
                    return;
                }

                if (queueIngestor != null)
                {
                    return; //Already started
                }

                var rawConfiguration = rawEndpointFactory.CreateSendOnly(errorQueue);

                queueIngestor = await transportCustomization.InitializeQueueIngestor(errorQueue, transportSettings, OnMessage, errorHandlingPolicy, OnCriticalError).ConfigureAwait(false);

                dispatcher = await RawEndpoint.Create(rawConfiguration).ConfigureAwait(false);

                if (settings.ForwardErrorMessages)
                {
                    await ingestor.VerifyCanReachForwardingAddress(dispatcher).ConfigureAwait(false);
                }

                await queueIngestor.Start()
                  .ConfigureAwait(false);

                logger.Info("Ensure started. Infrastructure started");
            }
            finally
            {
                startStopSemaphore.Release();
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

        Task OnCriticalError(string failure, Exception exception)
        {
            logger.Warn($"OnCriticalError. '{failure}'", exception);
            return watchdog.OnFailure(failure);
        }

        async Task EnsureStopped(CancellationToken cancellationToken = default)
        {
            try
            {
                await startStopSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                if (queueIngestor == null)
                {
                    return; //Already stopped
                }
                var stoppable = queueIngestor;
                queueIngestor = null;
                await stoppable.Stop().ConfigureAwait(false);
            }
            finally
            {
                startStopSemaphore.Release();
            }
        }

        SemaphoreSlim startStopSemaphore = new SemaphoreSlim(1);
        string errorQueue;
        RawEndpointFactory rawEndpointFactory;
        ErrorIngestionFaultPolicy errorHandlingPolicy;
        IQueueIngestor queueIngestor;
        IDispatchMessages dispatcher;

        readonly Settings settings;
        readonly TransportCustomization transportCustomization;
        readonly TransportSettings transportSettings;
        readonly Watchdog watchdog;
        readonly Channel<MessageContext> channel;
        readonly Task ingestionWorker;
        readonly Meter batchDurationMeter;
        readonly Meter batchSizeMeter;
        readonly Counter receivedMeter;
        readonly ErrorIngestor ingestor;
        readonly IIngestionUnitOfWorkFactory unitOfWorkFactory;
        static readonly ILog logger = LogManager.GetLogger<ErrorIngestion>();
    }
}