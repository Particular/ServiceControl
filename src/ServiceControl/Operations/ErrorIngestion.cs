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
    using Microsoft.Extensions.Options;
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
            IOptions<Settings> settingsOptions,
            ITransportCustomization transportCustomization,
            TransportSettings transportSettings,
            Metrics metrics,
            IErrorMessageDataStore dataStore,
            ErrorIngestionCustomCheck.State ingestionState,
            ErrorIngestor ingestor,
            IIngestionUnitOfWorkFactory unitOfWorkFactory,
            IHostApplicationLifetime applicationLifetime,
            ILogger<ErrorIngestion> logger)
        {
            settings = settingsOptions.Value;
            this.transportCustomization = transportCustomization;
            this.transportSettings = transportSettings;
            errorQueue = settings.ServiceBus.ErrorQueue;
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

            errorHandlingPolicy = new ErrorIngestionFaultPolicy(dataStore, settings.Logging, OnCriticalError, logger);

            watchdog = new Watchdog(
                "failed message ingestion",
                EnsureStarted,
                EnsureStopped,
                ingestionState.ReportError,
                ingestionState.Clear,
                settings.ServiceControl.TimeToRestartErrorIngestionAfterFailure,
                logger
            );
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
                        while (channel.Reader.TryRead(out var context))
                        {
                            contexts.Add(context);
                        }

                        batchSizeMeter.Mark(contexts.Count);
                        using (batchDurationMeter.Measure())
                        {
                            await ingestor.Ingest(contexts, stoppingToken);
                        }
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
                            logger.LogInformation(e, "Batch cancelled");
                            break;
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
                        logger.LogInformation(e, "Shutdown cancelled");
                    }
                }
            }
        }

        async Task EnsureStarted(CancellationToken cancellationToken = default)
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
                    TransportTransactionMode.ReceiveOnly
                );

                messageReceiver = transportInfrastructure.Receivers[errorQueue];

                if (settings.ServiceControl.ForwardErrorMessages == true)
                {
                    await ingestor.VerifyCanReachForwardingAddress(cancellationToken);
                }

                await messageReceiver.StartReceive(cancellationToken);

                logger.LogInformation(LogMessages.StartedInfrastructure);
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

                logger.LogInformation(LogMessages.StoppedInfrastructure);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to stop infrastructure");
                throw;
            }
        }

        async Task OnMessage(MessageContext messageContext, CancellationToken cancellationToken)
        {
            if (settings.ServiceControl.MessageFilter != null && settings.ServiceControl.MessageFilter(messageContext))
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

        Task OnCriticalError(string failure, Exception exception)
        {
            logger.LogCritical(exception, "OnCriticalError. '{FailureMessage}'", failure);
            return watchdog.OnFailure(failure);
        }

        async Task EnsureStopped(CancellationToken cancellationToken = default)
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

        SemaphoreSlim startStopSemaphore = new(1);
        string errorQueue;
        ErrorIngestionFaultPolicy errorHandlingPolicy;
        TransportInfrastructure transportInfrastructure;
        IMessageReceiver messageReceiver;

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