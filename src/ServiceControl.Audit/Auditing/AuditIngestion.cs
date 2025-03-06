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
            IHostApplicationLifetime applicationLifetime,
            AuditIngestionMetrics metrics)
        {
            inputEndpoint = settings.AuditQueue;
            this.transportCustomization = transportCustomization;
            this.transportSettings = transportSettings;
            this.auditIngestor = auditIngestor;
            this.unitOfWorkFactory = unitOfWorkFactory;
            this.settings = settings;
            this.applicationLifetime = applicationLifetime;
            this.metrics = metrics;

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

        async Task EnsureStarted(CancellationToken cancellationToken = default)
        {
            try
            {
                logger.Debug("Ensure started. Start/stop semaphore acquiring");
                await startStopSemaphore.WaitAsync(cancellationToken);
                logger.Debug("Ensure started. Start/stop semaphore acquired");

                if (!unitOfWorkFactory.CanIngestMore())
                {
                    if (queueIngestor != null)
                    {
                        var stoppable = queueIngestor;
                        queueIngestor = null;
                        logger.Info("Shutting down due to failed persistence health check. Infrastructure shut down commencing");
                        await stoppable.StopReceive(cancellationToken);
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

                transportInfrastructure = await transportCustomization.CreateTransportInfrastructure(
                    inputEndpoint,
                    transportSettings,
                    OnMessage,
                    errorHandlingPolicy.OnError,
                    OnCriticalError,
                    TransportTransactionMode.ReceiveOnly);

                queueIngestor = transportInfrastructure.Receivers[inputEndpoint];

                await auditIngestor.VerifyCanReachForwardingAddress();

                await queueIngestor.StartReceive(cancellationToken);

                logger.Info("Ensure started. Infrastructure started");
            }
            catch
            {
                if (queueIngestor != null)
                {
                    try
                    {
                        await queueIngestor.StopReceive(cancellationToken);
                    }
                    catch (OperationCanceledException e) when (e.CancellationToken == cancellationToken)
                    {
                        logger.Info("StopReceive cancelled");
                    }
                }

                queueIngestor = null; // Setting to null so that it doesn't exit when it retries in line 185

                throw;
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
                await startStopSemaphore.WaitAsync(cancellationToken);
                logger.Info("Shutting down. Start/stop semaphore acquired");

                if (queueIngestor == null)
                {
                    logger.Info("Shutting down. Already stopped, skipping shut down");
                    return; //Already stopped
                }

                var stoppable = queueIngestor;
                queueIngestor = null;
                logger.Info("Shutting down. Infrastructure shut down commencing");
                await stoppable.StopReceive(cancellationToken);
                logger.Info("Shutting down. Infrastructure shut down completed");
            }
            catch (OperationCanceledException e) when (e.CancellationToken == cancellationToken)
            {
                logger.Info("StopReceive cancelled");
            }
            finally
            {
                logger.Info("Shutting down. Start/stop semaphore releasing");
                startStopSemaphore.Release();
                logger.Info("Shutting down. Start/stop semaphore released");
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

            await channel.Writer.WriteAsync(messageContext, cancellationToken);
            _ = await taskCompletionSource.Task;

            messageIngestionMetrics.Success();
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await watchdog.Start(() => applicationLifetime.StopApplication());
            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                var contexts = new List<MessageContext>(MaxBatchSize);

                while (await channel.Reader.WaitToReadAsync(stoppingToken))
                {
                    // will only enter here if there is something to read.
                    try
                    {
                        // as long as there is something to read this will fetch up to MaximumConcurrency items
                        using (var batchMetrics = metrics.BeginBatch(MaxBatchSize))
                        {
                            while (channel.Reader.TryRead(out var context))
                            {
                                contexts.Add(context);
                            }

                            await auditIngestor.Ingest(contexts);

                            batchMetrics.Complete(contexts.Count);
                        }

                        //metrics.ClearB .Record(0);
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
                        //consecutiveBatchFailuresCounter.Record(consecutiveBatchFailures++);
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
                await watchdog.Stop();
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
        IMessageReceiver queueIngestor;

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
        readonly AuditIngestionMetrics metrics;

        static readonly ILog logger = LogManager.GetLogger<AuditIngestion>();
    }
}