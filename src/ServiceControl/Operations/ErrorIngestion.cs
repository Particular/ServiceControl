namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Linq;
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
            IHostApplicationLifetime applicationLifetime,
            ErrorQueueDiscoveryExecutor errorQueueDiscoveryExecutor)
        {
            this.settings = settings;
            this.transportCustomization = transportCustomization;
            this.transportSettings = transportSettings;
            this.ingestor = ingestor;
            this.unitOfWorkFactory = unitOfWorkFactory;
            this.applicationLifetime = applicationLifetime;
            this.errorQueueDiscoveryExecutor = errorQueueDiscoveryExecutor;
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
            if (messageReceivers != null)
            {
                Logger.Debug("Infrastructure already Started");
                return;
            }

            try
            {
                resolvers = await errorQueueDiscoveryExecutor.GetErrorQueueNamesAndReturnQueueResolvers(cancellationToken);

                if (!resolvers.Any())
                {
                    throw new Exception("No error queues found. Please check your configuration.");
                }

                Logger.Info("Starting infrastructure");
                transportInfrastructure = await transportCustomization.CreateTransportInfrastructure(
                    "ErrorIngestion",
                    resolvers.Keys.ToArray(),
                    transportSettings,
                    OnMessage,
                    errorHandlingPolicy.OnError,
                    OnCriticalError,
                    TransportTransactionMode.ReceiveOnly
                );

                messageReceivers = transportInfrastructure.Receivers.Values.ToArray();

                if (settings.ForwardErrorMessages)
                {
                    await ingestor.VerifyCanReachForwardingAddress(cancellationToken);
                }

                var startReceiveTasks = new Task[messageReceivers.Length];

                for (var i = 0; i < messageReceivers.Length; i++)
                {
                    startReceiveTasks[i] = messageReceivers[i].StartReceive(cancellationToken);
                }

                await Task.WhenAll(startReceiveTasks);

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
                    if (messageReceivers != null)
                    {
                        var stopReceiveTasks = new Task[messageReceivers.Length];

                        for (var i = 0; i < messageReceivers.Length; i++)
                        {
                            stopReceiveTasks[i] = messageReceivers[i].StopReceive(cancellationToken);
                        }

                        await Task.WhenAll(stopReceiveTasks);
                    }
                }
                finally
                {
                    await transportInfrastructure.Shutdown(cancellationToken);
                }

                messageReceivers = null;
                transportInfrastructure = null;

                Logger.Info(LogMessages.StoppedInfrastructure);
            }
            catch (Exception e)
            {
                Logger.Error("Failed to stop infrastructure", e);
                throw;
            }
        }

        async Task OnMessage(MessageContext messageContext, CancellationToken cancellationToken)
        {
            var resolver = resolvers[messageContext.ReceiveAddress];

            messageContext.Extensions.Set("ReturnQueueResolver", resolver);

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
        ErrorIngestionFaultPolicy errorHandlingPolicy;
        TransportInfrastructure transportInfrastructure;
        IMessageReceiver[] messageReceivers;
        Dictionary<string, ReturnQueueResolver> resolvers;

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
        readonly ErrorQueueDiscoveryExecutor errorQueueDiscoveryExecutor;
        static readonly ILog Logger = LogManager.GetLogger<ErrorIngestion>();

        internal static class LogMessages
        {
            internal const string StartedInfrastructure = "Started infrastructure";
            internal const string StoppedInfrastructure = "Stopped infrastructure";
        }
    }
}