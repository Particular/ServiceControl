namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure;
    using Metrics;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using NServiceBus.Transport;
    using Persistence;
    using Persistence.UnitOfWork;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.Ingestion;
    using Transports;

    class ErrorIngestion : BackgroundService
    {
        public ErrorIngestion(
            Settings settings,
            ITransportCustomization transportCustomization,
            TransportSettings transportSettings,
            IngestionMetrics metrics,
            IFailedErrorImportDataStore dataStore,
            ErrorIngestionCustomCheck.State ingestionState,
            ErrorIngestor ingestor,
            IIngestionUnitOfWorkFactory unitOfWorkFactory,
            IHostApplicationLifetime applicationLifetime,
            ILogger<ErrorIngestion> logger)
        {
            this.settings = settings;
            this.transportCustomization = transportCustomization;
            this.transportSettings = transportSettings;
            errorQueue = settings.ErrorQueue;
            this.ingestor = ingestor;
            this.unitOfWorkFactory = unitOfWorkFactory;
            this.applicationLifetime = applicationLifetime;
            this.metrics = metrics;
            this.logger = logger;

            if (!transportSettings.MaxConcurrency.HasValue)
            {
                throw new ArgumentException("MaxConcurrency is not set in TransportSettings");
            }

            MaxBatchSize = settings.ErrorIngestionBatchSize ?? transportSettings.MaxConcurrency.Value;

            pipeline = new IngestionPipeline(
                new IngestionPipelineSettings
                {
                    BatchSize = MaxBatchSize,
                    MaxWriters = IngestionSettingsReader.ResolveMaxParallelWriters(settings.ErrorIngestionMaxParallelWriters, unitOfWorkFactory.SupportsConcurrentBatches, nameof(settings.ErrorIngestionMaxParallelWriters), logger),
                    BatchTimeout = settings.ErrorIngestionBatchTimeout
                },
                IngestBatch,
                logger);

            errorHandlingPolicy = new ErrorIngestionFaultPolicy(dataStore, settings.LoggingSettings, OnCriticalError, metrics, logger);

            watchdog = new Watchdog(
                "failed message ingestion",
                EnsureStarted,
                EnsureStopped,
                ingestionState.ReportError,
                ingestionState.Clear,
                settings.TimeToRestartErrorIngestionAfterFailure,
                logger
            );
        }

        public override async Task StartAsync(CancellationToken cancellationToken = default)
        {
            await watchdog.Start(() => applicationLifetime.StopApplication(), cancellationToken);
            await base.StartAsync(cancellationToken);
        }

        protected override Task ExecuteAsync(CancellationToken cancellationToken = default) => pipeline.Run(cancellationToken);

        async Task IngestBatch(List<MessageContext> contexts, CancellationToken cancellationToken)
        {
            // Leaving the scope without completing it is what records the batch as failed
            using var batchMetrics = metrics.BeginBatch(MaxBatchSize);

            await ingestor.Ingest(contexts, messageDispatcher, cancellationToken);

            batchMetrics.Complete(contexts.Count);
        }

        public override async Task StopAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Order matters. Receiving stops first so nothing new enters the pipeline, but the
                // infrastructure is left running while the pipeline drains, because those batches
                // still forward through its dispatcher and Shutdown disposes it.
                await EnsureReceivingStopped(cancellationToken);
                pipeline.CompleteAdding();
                await base.StopAsync(cancellationToken);
            }
            finally
            {
                // Tears the infrastructure down, now that nothing is left to dispatch.
                await watchdog.Stop(cancellationToken);
            }
        }

        async Task EnsureStarted(CancellationToken cancellationToken)
        {
            try
            {
                await startStopSemaphore.WaitAsync(cancellationToken);

                var canIngest = unitOfWorkFactory.CanIngestMore();

                logger.LogDebug("Ensure started {CanIngest}", canIngest);

                if (canIngest)
                {
                    await SetUpAndStartInfrastructure(cancellationToken);
                }
                else
                {
                    await StopAndTeardownInfrastructure(cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                try
                {
                    await StopAndTeardownInfrastructure(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
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
                logger.LogDebug("Infrastructure already Started");
                return;
            }

            try
            {
                logger.LogInformation("Starting infrastructure");
                transportInfrastructure = await transportCustomization.CreateTransportInfrastructure(
                    errorQueue,
                    transportSettings,
                    OnMessage,
                    errorHandlingPolicy.OnError,
                    OnCriticalError,
                    TransportTransactionMode.ReceiveOnly,
                    cancellationToken
                );

                messageReceiver = transportInfrastructure.Receivers[errorQueue];
                messageDispatcher = transportInfrastructure.Dispatcher;

                if (settings.ForwardErrorMessages)
                {
                    await ingestor.VerifyCanReachForwardingAddress(messageDispatcher, cancellationToken);
                }

                await messageReceiver.StartReceive(cancellationToken);

                logger.LogInformation(LogMessages.StartedInfrastructure);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to start infrastructure");
                throw;
            }
        }
        async Task StopAndTeardownInfrastructure(CancellationToken cancellationToken)
        {
            if (transportInfrastructure == null)
            {
                logger.LogDebug("Infrastructure already Stopped");
                return;
            }
            try
            {
                logger.LogInformation("Stopping infrastructure");
                try
                {
                    await StopReceiving(cancellationToken);
                }
                finally
                {
                    await transportInfrastructure.Shutdown(cancellationToken);
                }

                messageReceiver = null;
                transportInfrastructure = null;
                receiveStopped = false;

                logger.LogInformation(LogMessages.StoppedInfrastructure);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to stop infrastructure");
                throw;
            }
        }

        async Task OnMessage(MessageContext messageContext, CancellationToken cancellationToken)
        {
            using var messageIngestionMetrics = metrics.BeginIngestion(messageContext);

            if (settings.MessageFilter != null && settings.MessageFilter(messageContext))
            {
                messageIngestionMetrics.Skipped();
                return;
            }

            var taskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            messageContext.SetTaskCompletionSource(taskCompletionSource);

            // Ideally we want to propagate the cancellationToken to the batch handling
            // but cancellation in only cancelled when endpointInstance.Stop is cancelled, not when invoked.
            // Not much shutdown speed to gain but this will ensure endpoint.Stop will return.
            await using var cancellationTokenRegistration = cancellationToken.Register(() => _ = taskCompletionSource.TrySetCanceled());

            await pipeline.Enqueue(messageContext, cancellationToken);
            await taskCompletionSource.Task;

            messageIngestionMetrics.Success();
        }

        Task OnCriticalError(string failure, Exception exception, CancellationToken cancellationToken)
        {
            logger.LogCritical(exception, "OnCriticalError. '{FailureMessage}'", failure);
            return watchdog.OnFailure(failure, cancellationToken);
        }

        async Task EnsureStopped(CancellationToken cancellationToken)
        {
            try
            {
                await startStopSemaphore.WaitAsync(cancellationToken);

                // By passing a CancellationToken in the cancelled state we stop receivers ASAP and
                // still correctly stop/shutdown
                await StopAndTeardownInfrastructure(new CancellationToken(canceled: true));
            }
            finally
            {
                startStopSemaphore.Release();
            }
        }

        async Task EnsureReceivingStopped(CancellationToken cancellationToken)
        {
            await startStopSemaphore.WaitAsync(cancellationToken);

            try
            {
                await StopReceiving(cancellationToken);
            }
            finally
            {
                startStopSemaphore.Release();
            }
        }

        // Stops the receiver on its own, leaving the infrastructure up. Idempotent because a
        // shutdown stops receiving before draining and then tears down, so this runs twice.
        async Task StopReceiving(CancellationToken cancellationToken)
        {
            if (messageReceiver == null || receiveStopped)
            {
                return;
            }

            await messageReceiver.StopReceive(cancellationToken);
            receiveStopped = true;
        }

        SemaphoreSlim startStopSemaphore = new(1);
        string errorQueue;
        ErrorIngestionFaultPolicy errorHandlingPolicy;
        TransportInfrastructure transportInfrastructure;
        IMessageReceiver messageReceiver;
        bool receiveStopped;

        // Left in place when the infrastructure is torn down. A shutdown drains before tearing down,
        // so this is still usable there
        IMessageDispatcher messageDispatcher;

        readonly Settings settings;
        readonly ITransportCustomization transportCustomization;
        readonly TransportSettings transportSettings;
        readonly int MaxBatchSize;
        readonly Watchdog watchdog;
        readonly IngestionPipeline pipeline;
        readonly IngestionMetrics metrics;
        readonly ErrorIngestor ingestor;
        readonly IIngestionUnitOfWorkFactory unitOfWorkFactory;
        readonly IHostApplicationLifetime applicationLifetime;

        readonly ILogger<ErrorIngestion> logger;

        public override void Dispose()
        {
            startStopSemaphore.Dispose();
            base.Dispose();
        }

        internal static class LogMessages
        {
            internal const string StartedInfrastructure = "Started infrastructure";
            internal const string StoppedInfrastructure = "Stopped infrastructure";
        }
    }
}