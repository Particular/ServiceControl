namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
    using ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;
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
            IngestionThrottleState throttleState,
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
            this.throttleState = throttleState;
            this.logger = logger;

            BatchSize = settings.AuditIngestionBatchSize;
            MaxParallelWriters = settings.AuditIngestionMaxParallelWriters;
            BatchTimeout = settings.AuditIngestionBatchTimeout;

            // Message channel: larger buffer to decouple transport from batch assembly
            int messageChannelCapacity = BatchSize * MaxParallelWriters * 2;
            messageChannel = Channel.CreateBounded<MessageContext>(new BoundedChannelOptions(messageChannelCapacity)
            {
                SingleReader = true,   // Batch assembler is single reader
                SingleWriter = false,  // Transport threads write concurrently
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });

            // Batch channel: holds assembled batches for parallel writers
            int batchChannelCapacity = MaxParallelWriters * 2;
            batchChannel = Channel.CreateBounded<List<MessageContext>>(new BoundedChannelOptions(batchChannelCapacity)
            {
                SingleReader = false,  // Multiple writers consume concurrently
                SingleWriter = true,   // Batch assembler is single writer
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

        Task OnCriticalError(string failure, Exception exception)
        {
            logger.LogCritical(exception, "OnCriticalError. '{Failure}'", failure);
            return watchdog.OnFailure(failure);
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
                    inputEndpoint,
                    transportSettings,
                    OnMessage,
                    errorHandlingPolicy.OnError,
                    OnCriticalError,
                    TransportTransactionMode.ReceiveOnly
                );

                messageReceiver = transportInfrastructure.Receivers[inputEndpoint];

                await auditIngestor.VerifyCanReachForwardingAddress(cancellationToken);
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

            await messageChannel.Writer.WriteAsync(messageContext, cancellationToken);
            _ = await taskCompletionSource.Task;

            messageIngestionMetrics.Success();
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await watchdog.Start(() => applicationLifetime.StopApplication(), cancellationToken);
            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Start batch assembler task
            // Note: Pass CancellationToken.None to Task.Run - if stoppingToken is already cancelled
            // it would throw immediately without starting the task. Let the loop handle cancellation internally.
            batchAssemblerTask = Task.Run(() => BatchAssemblerLoop(stoppingToken), CancellationToken.None);

            // Start parallel writer tasks
            writerTasks = new Task[MaxParallelWriters];
            for (int i = 0; i < MaxParallelWriters; i++)
            {
                int writerId = i;
                writerTasks[i] = Task.Run(() => WriterLoop(writerId, stoppingToken), CancellationToken.None);
            }

            try
            {
                await Task.WhenAll(writerTasks.Append(batchAssemblerTask));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown
            }
        }

        async Task BatchAssemblerLoop(CancellationToken stoppingToken)
        {
            var batch = new List<MessageContext>(BatchSize);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    // Wait for at least one message
                    if (!await messageChannel.Reader.WaitToReadAsync(stoppingToken))
                    {
                        break; // Channel completed
                    }

                    // Drain available messages up to BatchSize
                    while (batch.Count < BatchSize && messageChannel.Reader.TryRead(out var context))
                    {
                        batch.Add(context);
                    }

                    // If batch is not full, wait with timeout for more messages
                    if (batch.Count > 0 && batch.Count < BatchSize)
                    {
                        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        timeoutCts.CancelAfter(BatchTimeout);

                        try
                        {
                            while (batch.Count < BatchSize)
                            {
                                if (!await messageChannel.Reader.WaitToReadAsync(timeoutCts.Token))
                                {
                                    break; // Channel completed
                                }

                                while (batch.Count < BatchSize && messageChannel.Reader.TryRead(out var context))
                                {
                                    batch.Add(context);
                                }
                            }
                        }
                        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                        {
                            // Timeout reached, dispatch partial batch
                        }
                    }

                    if (batch.Count > 0)
                    {
                        await batchChannel.Writer.WriteAsync(batch, stoppingToken);
                        batch = new List<MessageContext>(BatchSize);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown
            }
            finally
            {
                // Complete the batch channel to signal writers to finish
                batchChannel.Writer.Complete();

                // Cancel any remaining messages in incomplete batch
                foreach (var context in batch)
                {
                    _ = context.GetTaskCompletionSource().TrySetCanceled(stoppingToken);
                }
            }
        }

        async Task WriterLoop(int writerId, CancellationToken stoppingToken)
        {
            logger.LogDebug("Writer {WriterId} started", writerId);
            List<MessageContext> currentBatch = null;

            try
            {
                await foreach (var batch in batchChannel.Reader.ReadAllAsync(stoppingToken))
                {
                    var currentBatchRef = batch;

                    // Check if this writer should yield due to throttling
                    // Writers with higher IDs yield first (writer 3 yields before writer 2, etc.)
                    var currentLimit = throttleState.GetActiveWriterLimit(
                        MaxParallelWriters,
                        settings.CleanupThrottleInterval,
                        settings.MinWritersDuringCleanup);

                    while (writerId >= currentLimit && !stoppingToken.IsCancellationRequested)
                    {
                        logger.LogDebug("Writer {WriterId} yielding due to throttle (limit: {Limit})",
                            writerId, currentLimit);

                        // Put batch back for an active writer to handle
                        await batchChannel.Writer.WriteAsync(currentBatchRef, stoppingToken);

                        // Wait before checking if throttle has lifted
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

                        // Re-check limit (may have changed or cleanup may have ended)
                        currentLimit = throttleState.GetActiveWriterLimit(
                            MaxParallelWriters,
                            settings.CleanupThrottleInterval,
                            settings.MinWritersDuringCleanup);

                        // If still throttled, try to get a batch (another writer may have taken ours)
                        if (writerId >= currentLimit)
                        {
                            if (!batchChannel.Reader.TryRead(out currentBatchRef))
                            {
                                // No batch available, wait for next one from the outer loop
                                currentBatchRef = null;
                                break;
                            }
                        }
                    }

                    if (currentBatchRef == null)
                    {
                        continue;
                    }

                    currentBatch = currentBatchRef;
                    try
                    {
                        using var batchMetrics = metrics.BeginBatch(BatchSize);

                        await auditIngestor.Ingest(currentBatch, stoppingToken);

                        batchMetrics.Complete(currentBatch.Count);
                        currentBatch = null; // Successfully processed
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        // Signal failure to all messages in this batch
                        foreach (var context in currentBatch)
                        {
                            _ = context.GetTaskCompletionSource().TrySetException(e);
                        }

                        currentBatch = null; // Failure handled
                        logger.LogWarning(e, "Writer {WriterId} failed to ingest batch", writerId);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown - signal cancellation for any in-flight batch
                if (currentBatch != null)
                {
                    foreach (var context in currentBatch)
                    {
                        _ = context.GetTaskCompletionSource().TrySetCanceled(stoppingToken);
                    }
                }
            }

            logger.LogDebug("Writer {WriterId} stopped", writerId);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await watchdog.Stop(cancellationToken);

                // Complete message channel to stop accepting new messages
                messageChannel.Writer.Complete();

                // Wait for batch assembler to finish (it completes the batch channel)
                if (batchAssemblerTask != null)
                {
                    await batchAssemblerTask.WaitAsync(cancellationToken);
                }

                // Wait for all writers to finish
                if (writerTasks != null)
                {
                    await Task.WhenAll(writerTasks).WaitAsync(cancellationToken);
                }

                await base.StopAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("Graceful shutdown timed out");
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

        public override void Dispose()
        {
            startStopSemaphore.Dispose();
            base.Dispose();
        }

        TransportInfrastructure transportInfrastructure;
        IMessageReceiver messageReceiver;
        Task batchAssemblerTask;
        Task[] writerTasks;

        readonly int BatchSize;
        readonly int MaxParallelWriters;
        readonly TimeSpan BatchTimeout;
        readonly SemaphoreSlim startStopSemaphore = new(1);
        readonly string inputEndpoint;
        readonly ITransportCustomization transportCustomization;
        readonly TransportSettings transportSettings;
        readonly AuditIngestor auditIngestor;
        readonly AuditIngestionFaultPolicy errorHandlingPolicy;
        readonly IAuditIngestionUnitOfWorkFactory unitOfWorkFactory;
        readonly Settings settings;
        readonly Channel<MessageContext> messageChannel;
        readonly Channel<List<MessageContext>> batchChannel;
        readonly Watchdog watchdog;
        readonly IHostApplicationLifetime applicationLifetime;
        readonly IngestionMetrics metrics;
        readonly IngestionThrottleState throttleState;
        readonly ILogger<AuditIngestion> logger;

        internal static class LogMessages
        {
            internal const string StartedInfrastructure = "Started infrastructure";
            internal const string StoppedInfrastructure = "Stopped infrastructure";
        }
    }
}