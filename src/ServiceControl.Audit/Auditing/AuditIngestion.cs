namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Infrastructure.Settings;
    using Metrics;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
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
            IHostApplicationLifetime applicationLifetime,
            IngestionMetrics metrics,
            ILogger<AuditIngestion> logger
        )
        {
            inputEndpoint = settings.AuditQueue;
            this.transportCustomization = transportCustomization;
            this.transportSettings = transportSettings;
            this.auditIngestor = auditIngestor;
            this.unitOfWorkFactory = unitOfWorkFactory;
            this.settings = settings;
            this.applicationLifetime = applicationLifetime;
            this.metrics = metrics;
            this.logger = logger;
            if (!transportSettings.MaxConcurrency.HasValue)
            {
                throw new ArgumentException("MaxConcurrency is not set in TransportSettings");
            }

            MaxBatchSize = transportSettings.MaxConcurrency.Value;

            channel = Channel.CreateBounded<MessageContext>(new BoundedChannelOptions(MaxBatchSize)
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });

            errorHandlingPolicy = new AuditIngestionFaultPolicy(failedImportsStorage, settings.LoggingSettings, OnCriticalError, metrics, logger);

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

        Task OnCriticalError(string failure, Exception exception, CancellationToken cancellationToken)
        {
            logger.LogCritical(exception, "OnCriticalError. '{Failure}'", failure);
            return watchdog.OnFailure(failure, cancellationToken);
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
                    inputEndpoint,
                    transportSettings,
                    OnMessage,
                    errorHandlingPolicy.OnError,
                    OnCriticalError,
                    TransportTransactionMode.ReceiveOnly,
                    cancellationToken: cancellationToken
                );

                messageReceiver = transportInfrastructure.Receivers[inputEndpoint];
                messageDispatcher = transportInfrastructure.Dispatcher;

                await auditIngestor.VerifyCanReachForwardingAddress(messageDispatcher, cancellationToken);
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

            await channel.Writer.WriteAsync(messageContext, cancellationToken);
            _ = await taskCompletionSource.Task;

            messageIngestionMetrics.Success();
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
                var contexts = new List<MessageContext>(MaxBatchSize);

                while (await channel.Reader.WaitToReadAsync(cancellationToken))
                {
                    // will only enter here if there is something to read.
                    try
                    {
                        using var batchMetrics = metrics.BeginBatch(MaxBatchSize);

                        // as long as there is something to read this will fetch up to MaximumConcurrency items
                        while (channel.Reader.TryRead(out var context))
                        {
                            contexts.Add(context);
                        }

                        await auditIngestor.Ingest(contexts, messageDispatcher, cancellationToken);

                        batchMetrics.Complete(contexts.Count);
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
                // Order matters. Receiving stops under the shutdown token rather than a cancelled
                // one, so messages already being processed finish and their receives commit instead
                // of being abandoned and redelivered after having been forwarded. Nothing new enters
                // the channel after this, and the infrastructure stays up until the channel drains.
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

        TransportInfrastructure transportInfrastructure;
        IMessageReceiver messageReceiver;
        bool receiveStopped;

        // Left in place when the infrastructure is torn down. A shutdown drains before tearing down,
        // so this is still usable there.
        IMessageDispatcher messageDispatcher;

        readonly int MaxBatchSize;
        readonly SemaphoreSlim startStopSemaphore = new(1);
        readonly string inputEndpoint;
        readonly ITransportCustomization transportCustomization;
        readonly TransportSettings transportSettings;
        readonly AuditIngestor auditIngestor;
        readonly AuditIngestionFaultPolicy errorHandlingPolicy;
        readonly IAuditIngestionUnitOfWorkFactory unitOfWorkFactory;
        readonly Settings settings;
        readonly Channel<MessageContext> channel;
        readonly Watchdog watchdog;
        readonly IHostApplicationLifetime applicationLifetime;
        readonly IngestionMetrics metrics;
        readonly ILogger<AuditIngestion> logger;

        internal static class LogMessages
        {
            internal const string StartedInfrastructure = "Started infrastructure";
            internal const string StoppedInfrastructure = "Stopped infrastructure";
        }
    }
}