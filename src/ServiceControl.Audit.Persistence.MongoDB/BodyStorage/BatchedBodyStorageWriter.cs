namespace ServiceControl.Audit.Persistence.MongoDB.BodyStorage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    abstract class BatchedBodyStorageWriter<TEntry>(
        Channel<TEntry> channel,
        MongoSettings settings,
        ILogger logger)
        : BackgroundService
    {
        readonly int BatchSize = settings.BodyWriterBatchSize;
        readonly int ParallelWriters = settings.BodyWriterParallelWriters;
        readonly TimeSpan BatchTimeout = settings.BodyWriterBatchTimeout;
        const int BacklogWarningThreshold = 5_000;
        long totalWritten;
        DateTime lastBacklogWarning;
        DateTime lastBackpressureWarning;

        readonly Channel<List<TEntry>> batchChannel = Channel.CreateBounded<List<TEntry>>(
            new BoundedChannelOptions(settings.BodyWriterParallelWriters * 2)
            {
                SingleReader = false,
                SingleWriter = true,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });

        protected ChannelWriter<TEntry> WriteChannel => channel.Writer;

        protected async ValueTask WriteToChannelAsync(TEntry entry, CancellationToken cancellationToken)
        {
            if (channel.Writer.TryWrite(entry))
            {
                return;
            }

            if (DateTime.UtcNow - lastBackpressureWarning > TimeSpan.FromSeconds(10))
            {
                lastBackpressureWarning = DateTime.UtcNow;
                logger.LogWarning("{WriterName} channel is full (backlog: {Backlog}). Body writes are blocking ingestion until the writer catches up",
                    WriterName, channel.Reader.Count);
            }

            await channel.Writer.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
        }

        protected abstract string WriterName { get; }

        protected abstract Task FlushBatchAsync(List<TEntry> batch, CancellationToken cancellationToken);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("{WriterName} started ({Writers} writers, batch size {BatchSize})", WriterName, ParallelWriters, BatchSize);

            var assemblerTask = Task.Run(() => BatchAssemblerLoop(stoppingToken), CancellationToken.None);

            var writerTasks = new Task[ParallelWriters];
            for (var i = 0; i < ParallelWriters; i++)
            {
                var writerId = i;
                writerTasks[i] = Task.Run(() => WriterLoop(writerId, stoppingToken), CancellationToken.None);
            }

            try
            {
                await Task.WhenAll(writerTasks.Append(assemblerTask)).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown
            }

            logger.LogInformation("{WriterName} stopped", WriterName);
        }

        async Task BatchAssemblerLoop(CancellationToken stoppingToken)
        {
            var batch = new List<TEntry>(BatchSize);

            try
            {
                while (await channel.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
                {
                    while (batch.Count < BatchSize && channel.Reader.TryRead(out var entry))
                    {
                        batch.Add(entry);
                    }

                    if (batch.Count > 0 && batch.Count < BatchSize)
                    {
                        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        timeoutCts.CancelAfter(BatchTimeout);
                        try
                        {
                            while (batch.Count < BatchSize)
                            {
                                if (!await channel.Reader.WaitToReadAsync(timeoutCts.Token).ConfigureAwait(false))
                                {
                                    break;
                                }

                                while (batch.Count < BatchSize && channel.Reader.TryRead(out var entry))
                                {
                                    batch.Add(entry);
                                }
                            }
                        }
                        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                        {
                            // Timeout expired - dispatch partial batch
                        }
                    }

                    if (batch.Count > 0)
                    {
                        await batchChannel.Writer.WriteAsync(batch, stoppingToken).ConfigureAwait(false);
                        batch = new List<TEntry>(BatchSize);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutting down - drain channel into remaining batches
                while (channel.Reader.TryRead(out var entry))
                {
                    batch.Add(entry);

                    if (batch.Count >= BatchSize)
                    {
                        await batchChannel.Writer.WriteAsync(batch, CancellationToken.None).ConfigureAwait(false);
                        batch = new List<TEntry>(BatchSize);
                    }
                }

                if (batch.Count > 0)
                {
                    await batchChannel.Writer.WriteAsync(batch, CancellationToken.None).ConfigureAwait(false);
                }
            }
            finally
            {
                batchChannel.Writer.Complete();
            }
        }

        async Task WriterLoop(int writerId, CancellationToken stoppingToken)
        {
            logger.LogDebug("{WriterName} writer {WriterId} started", WriterName, writerId);

            try
            {
                // Use CancellationToken.None for FlushBatch so in-flight writes complete
                // during shutdown. ReadAllAsync(stoppingToken) controls when we stop
                // accepting new batches.
                await foreach (var batch in batchChannel.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
                {
                    await FlushBatchAsync(batch, CancellationToken.None).ConfigureAwait(false);
                    ReportBatchWritten(batch.Count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown
            }

            // Drain any remaining batches after the assembler completes the channel
            while (batchChannel.Reader.TryRead(out var batch))
            {
                try
                {
                    await FlushBatchAsync(batch, CancellationToken.None).ConfigureAwait(false);
                    ReportBatchWritten(batch.Count);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to flush {Count} entries during shutdown", batch.Count);
                }
            }

            logger.LogDebug("{WriterName} writer {WriterId} stopped", WriterName, writerId);
        }

        void ReportBatchWritten(int batchCount)
        {
            totalWritten += batchCount;
            var backlog = channel.Reader.Count;
            logger.LogDebug("{WriterName}: batch={BatchCount}, total={TotalWritten}, backlog={Backlog}",
                WriterName, batchCount, totalWritten, backlog);
            if (backlog > BacklogWarningThreshold && DateTime.UtcNow - lastBacklogWarning > TimeSpan.FromSeconds(10))
            {
                lastBacklogWarning = DateTime.UtcNow;
                logger.LogWarning("{WriterName} is not keeping up with ingestion. Channel backlog: {Backlog} items", WriterName, backlog);
            }
        }
    }
}
