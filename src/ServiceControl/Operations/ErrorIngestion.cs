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
    using NServiceBus;
    using NServiceBus.Logging;
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
            IErrorMessageDataStore dataStore,
            ErrorIngestionCustomCheck.State ingestionState,
            ErrorIngestor ingestor,
            IIngestionUnitOfWorkFactory unitOfWorkFactory,
            IHostApplicationLifetime applicationLifetime)
        {
            this.settings = settings;
            this.transportCustomization = transportCustomization;
            this.transportSettings = transportSettings;
            errorQueue = settings.ErrorQueue;
            this.ingestor = ingestor;
            this.unitOfWorkFactory = unitOfWorkFactory;
            this.applicationLifetime = applicationLifetime;

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

            errorHandlingPolicy = new ErrorIngestionFaultPolicy(dataStore, settings.LoggingSettings, OnCriticalError);

            watchdog = new Watchdog(
                "failed message ingestion",
                EnsureStarted,
                EnsureStopped,
                ingestionState.ReportError,
                ingestionState.Clear,
                settings.TimeToRestartErrorIngestionAfterFailure,
                Logger
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
                            Logger.Info("Batch cancelled", e);
                            break;
                        }

                        Logger.Info("Ingesting messages failed", e);
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
                await stoppingToken.CancelAsync();
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
                        Logger.Info("Shutdown cancelled", e);
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

                Logger.DebugFormat("Ensure started {0}", canIngest);

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
                Logger.Debug("Infrastructure already Started");
                return;
            }

            try
            {
                Logger.Info("Starting infrastructure");
                transportInfrastructure = await transportCustomization.CreateTransportInfrastructure(
                    errorQueue,
                    transportSettings,
                    OnMessage,
                    errorHandlingPolicy.OnError,
                    OnCriticalError,
                    TransportTransactionMode.ReceiveOnly
                );

                messageReceiver = transportInfrastructure.Receivers[errorQueue];

                if (settings.ForwardErrorMessages)
                {
                    await ingestor.VerifyCanReachForwardingAddress(cancellationToken);
                }

                await messageReceiver.StartReceive(cancellationToken);

                Logger.Info(LogMessages.StartedInfrastructure);
            }
            catch (Exception e)
            {
                Logger.Error("Failed to start infrastructure", e);
                throw;
            }
        }
        async Task StopAndTeardownInfrastructure(CancellationToken cancellationToken)
        {
            if (transportInfrastructure == null)
            {
                Logger.Debug("Infrastructure already Stopped");
                return;
            }
            try
            {
                Logger.Info("Stopping infrastructure");
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

                Logger.Info(LogMessages.StoppedInfrastructure);
            }
            catch (Exception e)
            {
                Logger.Error("Failed to stop infrastructure", e);
                throw;
            }
        }

        async Task OnMessage(MessageContext messageContext, CancellationToken cancellationTokenParent)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenParent, stoppingToken.Token);
            var cancellationToken = cts.Token;

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

        Task OnCriticalError(string failure, Exception exception)
        {
            Logger.Fatal($"OnCriticalError. '{failure}'", exception);
            return watchdog.OnFailure(failure);
        }

        async Task EnsureStopped(CancellationToken cancellationToken = default)
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

        SemaphoreSlim startStopSemaphore = new(1);
        string errorQueue;
        ErrorIngestionFaultPolicy errorHandlingPolicy;
        TransportInfrastructure transportInfrastructure;
        IMessageReceiver messageReceiver;

        readonly CancellationTokenSource stoppingToken = new();
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

        static readonly ILog Logger = LogManager.GetLogger<ErrorIngestion>();

        internal static class LogMessages
        {
            internal const string StartedInfrastructure = "Started infrastructure";
            internal const string StoppedInfrastructure = "Stopped infrastructure";
        }
    }
}