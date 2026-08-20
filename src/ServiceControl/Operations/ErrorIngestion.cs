namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.Metrics;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using NServiceBus.Transport;
    using Persistence;
    using Persistence.UnitOfWork;
    using ServiceBus.Management.Infrastructure.Settings;
    using Transports;

    class ErrorIngestion : BackgroundService
    {
        static readonly long FrequencyInMilliseconds = Stopwatch.Frequency / 1000;

        public ErrorIngestion(
            Settings settings,
            ITransportCustomization transportCustomization,
            TransportSettings transportSettings,
            Metrics metrics,
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
            this.logger = logger;
            receivedMeter = metrics.GetCounter("Error ingestion - received");
            batchSizeMeter = metrics.GetMeter("Error ingestion - batch size");
            batchDurationMeter = metrics.GetMeter("Error ingestion - batch processing duration", FrequencyInMilliseconds);

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

            errorHandlingPolicy = new ErrorIngestionFaultPolicy(dataStore, settings.LoggingSettings, OnCriticalError, logger);

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

        protected override async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var contexts = new List<MessageContext>(transportSettings.MaxConcurrency.Value);

                while (await channel.Reader.WaitToReadAsync(cancellationToken))
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
                            await ingestor.Ingest(contexts, messageDispatcher, cancellationToken);
                        }
                    }
                    catch (OperationCanceledException e) when (cancellationToken.IsCancellationRequested)
                    {
                        // signal all message handling tasks to terminate
                        foreach (var context in contexts)
                        {
                            _ = context.GetTaskCompletionSource().TrySetException(e);
                        }

                        logger.LogInformation(e, "Batch cancelled");
                        break;
                    }
                    catch (Exception e)
                    {
                        // signal all message handling tasks to terminate
                        foreach (var context in contexts)
                        {
                            _ = context.GetTaskCompletionSource().TrySetException(e);
                        }

                        logger.LogInformation(e, "Ingesting messages failed");
                    }
                    finally
                    {
                        contexts.Clear();
                    }
                }
                // will fall out here when writer is completed
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // ExecuteAsync cancelled
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Order matters. Receiving stops first so nothing new enters the channel, but the
                // infrastructure is left running while the channel drains, because those batches
                // still forward through its dispatcher and Shutdown disposes it.
                await EnsureReceivingStopped(cancellationToken);
                channel.Writer.Complete();
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
            if (settings.MessageFilter != null && settings.MessageFilter(messageContext))
            {
                return;
            }

            var taskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            messageContext.SetTaskCompletionSource(taskCompletionSource);

            // Ideally we want to propagate the cancellationToken to the batch handling
            // but cancellation in only cancelled when endpointInstance.Stop is cancelled, not when invoked.
            // Not much shutdown speed to gain but this will ensure endpoint.Stop will return.
            await using var cancellationTokenRegistration = cancellationToken.Register(() => _ = taskCompletionSource.TrySetCanceled());

            receivedMeter.Mark();

            await channel.Writer.WriteAsync(messageContext, cancellationToken);
            await taskCompletionSource.Task;
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
        readonly Watchdog watchdog;
        readonly Channel<MessageContext> channel;
        readonly Meter batchDurationMeter;
        readonly Meter batchSizeMeter;
        readonly Counter receivedMeter;
        readonly ErrorIngestor ingestor;
        readonly IIngestionUnitOfWorkFactory unitOfWorkFactory;
        readonly IHostApplicationLifetime applicationLifetime;

        readonly ILogger<ErrorIngestion> logger;

        internal static class LogMessages
        {
            internal const string StartedInfrastructure = "Started infrastructure";
            internal const string StoppedInfrastructure = "Stopped infrastructure";
        }
    }
}