namespace ServiceControl.Infrastructure.Ingestion;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NServiceBus.Transport;

/// <summary>
/// Batches the messages a transport receiver hands over and writes those batches with a
/// configurable number of concurrent writers.
///
/// Receivers call <see cref="Enqueue" /> and then wait on the message's TaskCompletionSource, so a
/// receive commits only once the batch it landed in has been written. Batches are assembled by a
/// single reader, which keeps the writers fed without any of them competing over the message
/// channel, and both channels are bounded so a slow storage pushes back on the receivers rather
/// than growing a queue in memory.
/// </summary>
public sealed class IngestionPipeline
{
    public IngestionPipeline(
        IngestionPipelineSettings settings,
        Func<List<MessageContext>, CancellationToken, Task> ingest,
        ILogger logger)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.BatchSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.MaxWriters, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.BatchTimeout, TimeSpan.Zero);

        this.settings = settings;
        this.ingest = ingest;
        this.logger = logger;

        // Room for a second batch behind the one each writer is holding, so the receivers keep
        // being drained while every writer is mid flush.
        messageChannel = Channel.CreateBounded<MessageContext>(
            new BoundedChannelOptions(settings.BatchSize * settings.MaxWriters)
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });

        batchChannel = Channel.CreateBounded<List<MessageContext>>(
            new BoundedChannelOptions(settings.MaxWriters)
            {
                SingleReader = settings.MaxWriters == 1,
                SingleWriter = true,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });
    }

    public ValueTask Enqueue(MessageContext context, CancellationToken cancellationToken = default) =>
        messageChannel.Writer.WriteAsync(context, cancellationToken);

    /// <summary>
    /// Stops the pipeline taking messages. <see cref="Run" /> returns once what is already in it
    /// has been written.
    /// </summary>
    public void CompleteAdding() => messageChannel.Writer.Complete();

    public async Task Run(CancellationToken cancellationToken = default)
    {
        // Task.Run throws before the loop body runs at all when the token is already cancelled,
        // which would leave the channels with nobody to drain or abandon what is in them.
        var assembling = Task.Run(() => AssembleBatches(cancellationToken), CancellationToken.None);
        var writing = Enumerable
            .Range(0, settings.MaxWriters)
            .Select(_ => Task.Run(() => WriteBatches(cancellationToken), CancellationToken.None))
            .ToArray();

        try
        {
            await Task.WhenAll(writing.Append(assembling)).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            // Batches assembled but never picked up are abandoned for the same reason the assembler
            // abandons what it never dispatched.
            while (batchChannel.Reader.TryRead(out var batch))
            {
                Abandon(batch);
            }
        }
    }

    async Task AssembleBatches(CancellationToken cancellationToken)
    {
        var batch = new List<MessageContext>(settings.BatchSize);

        try
        {
            while (await messageChannel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                Drain(batch);

                if (batch.Count < settings.BatchSize && settings.BatchTimeout > TimeSpan.Zero)
                {
                    await WaitForBatchToFill(batch, cancellationToken).ConfigureAwait(false);
                }

                if (batch.Count > 0)
                {
                    await batchChannel.Writer.WriteAsync(batch, cancellationToken).ConfigureAwait(false);
                    batch = new List<MessageContext>(settings.BatchSize);
                }
            }
        }
        finally
        {
            batchChannel.Writer.Complete();

            // Cancellation leaves messages that never reached a writer. Abandoning them releases
            // the receives waiting on them, so the transport redelivers them instead of the
            // shutdown hanging on receives that will never be answered.
            Abandon(batch);

            while (messageChannel.Reader.TryRead(out var context))
            {
                Abandon(context);
            }
        }
    }

    void Drain(List<MessageContext> batch)
    {
        while (batch.Count < settings.BatchSize && messageChannel.Reader.TryRead(out var context))
        {
            batch.Add(context);
        }
    }

    async Task WaitForBatchToFill(List<MessageContext> batch, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.BatchTimeout);

        try
        {
            while (batch.Count < settings.BatchSize && await messageChannel.Reader.WaitToReadAsync(timeout.Token).ConfigureAwait(false))
            {
                Drain(batch);
            }
        }
        catch (OperationCanceledException) when (timeout.Token.IsCancellationRequested)
        {
            // The linked source is cancelled by a shutdown as well as by the wait expiring
            cancellationToken.ThrowIfCancellationRequested();

            // So this is the wait expiring, and the batch goes as it stands
        }
    }

    async Task WriteBatches(CancellationToken cancellationToken)
    {
        await foreach (var batch in batchChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                await ingest(batch, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException e) when (cancellationToken.IsCancellationRequested)
            {
                Fail(batch, e);

                logger.LogInformation(e, "Batch cancelled");
                throw;
            }
            catch (Exception e)
            {
                Fail(batch, e);

                logger.LogInformation(e, "Ingesting messages failed");
            }
        }
    }

    static void Fail(List<MessageContext> batch, Exception exception)
    {
        foreach (var context in batch)
        {
            _ = context.GetTaskCompletionSource().TrySetException(exception);
        }
    }

    static void Abandon(List<MessageContext> batch)
    {
        foreach (var context in batch)
        {
            Abandon(context);
        }
    }

    static void Abandon(MessageContext context) => _ = context.GetTaskCompletionSource().TrySetCanceled();

    readonly IngestionPipelineSettings settings;
    readonly Func<List<MessageContext>, CancellationToken, Task> ingest;
    readonly ILogger logger;
    readonly Channel<MessageContext> messageChannel;
    readonly Channel<List<MessageContext>> batchChannel;
}