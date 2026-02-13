namespace ServiceControl.Audit.Persistence.MongoDB.Search
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Collections;
    using Documents;
    using global::MongoDB.Driver;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    class BodyStorageWriter(
        Channel<BodyEntry> channel,
        IMongoClientProvider clientProvider,
        MongoSettings settings,
        ILogger<BodyStorageWriter> logger)
        : BackgroundService
    {
        readonly int BatchSize = settings.BodyWriterBatchSize;
        readonly int ParallelWriters = settings.BodyWriterParallelWriters;
        readonly TimeSpan BatchTimeout = settings.BodyWriterBatchTimeout;
        const int MaxRetries = 3;

        readonly Channel<List<MessageBodyDocument>> batchChannel = Channel.CreateBounded<List<MessageBodyDocument>>(
            new BoundedChannelOptions(settings.BodyWriterParallelWriters * 2)
            {
                SingleReader = false,
                SingleWriter = true,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Body storage writer started ({Writers} writers, batch size {BatchSize})", ParallelWriters, BatchSize);

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

            logger.LogInformation("Body storage writer stopped");
        }

        async Task BatchAssemblerLoop(CancellationToken stoppingToken)
        {
            var batch = new List<MessageBodyDocument>(BatchSize);

            try
            {
                while (await channel.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
                {
                    while (batch.Count < BatchSize && channel.Reader.TryRead(out var entry))
                    {
                        batch.Add(ToDocument(entry));
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
                                    batch.Add(ToDocument(entry));
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
                        batch = new List<MessageBodyDocument>(BatchSize);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutting down - drain channel into remaining batches
                while (channel.Reader.TryRead(out var entry))
                {
                    batch.Add(ToDocument(entry));

                    if (batch.Count >= BatchSize)
                    {
                        await batchChannel.Writer.WriteAsync(batch, CancellationToken.None).ConfigureAwait(false);
                        batch = new List<MessageBodyDocument>(BatchSize);
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
            logger.LogDebug("Body writer {WriterId} started", writerId);

            try
            {
                // Use CancellationToken.None for FlushBatch so in-flight writes complete
                // during shutdown. ReadAllAsync(stoppingToken) controls when we stop
                // accepting new batches.
                await foreach (var batch in batchChannel.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
                {
                    await FlushBatch(batch, CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown
            }

            // Drain any remaining batches after the assembler completes the channel
            while (batchChannel.Reader.TryRead(out var batch))
            {
                await FlushBatchBestEffort(batch).ConfigureAwait(false);
            }

            logger.LogDebug("Body writer {WriterId} stopped", writerId);
        }

        async Task FlushBatchBestEffort(List<MessageBodyDocument> batch)
        {
            try
            {
                await FlushBatch(batch, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to flush {Count} body entries during shutdown", batch.Count);
            }
        }

        async Task FlushBatch(List<MessageBodyDocument> batch, CancellationToken cancellationToken)
        {
            var collection = clientProvider.Database
                .GetCollection<MessageBodyDocument>(CollectionNames.MessageBodies);

            var writes = batch.Select(doc =>
                new ReplaceOneModel<MessageBodyDocument>(
                    Builders<MessageBodyDocument>.Filter.Eq(d => d.Id, doc.Id),
                    doc)
                { IsUpsert = true })
                .ToList();

            for (var attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    _ = await collection.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = false }, cancellationToken).ConfigureAwait(false);
                    logger.LogDebug("Wrote {Count} body entries", batch.Count);
                    return;
                }
                catch (Exception ex) when (attempt < MaxRetries && !cancellationToken.IsCancellationRequested)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                    logger.LogWarning(ex, "Failed to write {Count} body entries (attempt {Attempt}/{MaxRetries}), retrying in {Delay}s",
                        batch.Count, attempt, MaxRetries, delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to write {Count} body entries after {MaxRetries} attempts", batch.Count, MaxRetries);
                }
            }
        }

        static MessageBodyDocument ToDocument(BodyEntry entry) => new()
        {
            Id = entry.Id,
            ContentType = entry.ContentType,
            BodySize = entry.BodySize,
            TextBody = entry.TextBody,
            BinaryBody = entry.BinaryBody,
            ExpiresAt = entry.ExpiresAt
        };
    }
}
