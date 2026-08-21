namespace ServiceControl.Infrastructure.Tests.Ingestion;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NServiceBus.Extensibility;
using NServiceBus.Transport;
using NUnit.Framework;
using ServiceControl.Infrastructure.Ingestion;

[TestFixture]
class IngestionPipelineTests
{
    [Test]
    public async Task A_batch_never_holds_more_than_the_batch_size()
    {
        var batches = new ConcurrentQueue<int>();
        var pipeline = Build(new IngestionPipelineSettings { BatchSize = 2 }, batch =>
        {
            batches.Enqueue(batch.Count);
            Succeed(batch);
        });

        var messages = await Drain(pipeline, 5, TestContext.CurrentContext.CancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batches.Sum(), Is.EqualTo(5));
            Assert.That(batches, Has.All.LessThanOrEqualTo(2));
            Assert.That(messages.Select(message => message.GetTaskCompletionSource().Task.IsCompletedSuccessfully), Has.All.True);
        }
    }

    [Test]
    public async Task A_partial_batch_is_written_rather_than_waited_on_when_there_is_no_timeout()
    {
        var batches = new ConcurrentQueue<int>();
        var pipeline = Build(new IngestionPipelineSettings { BatchSize = 100 }, batch =>
        {
            batches.Enqueue(batch.Count);
            Succeed(batch);
        });

        await Drain(pipeline, 1, TestContext.CurrentContext.CancellationToken);

        Assert.That(batches, Is.EqualTo(new[] { 1 }));
    }

    [Test]
    public async Task A_partial_batch_waits_to_be_filled()
    {
        var batches = new ConcurrentQueue<int>();
        var pipeline = Build(
            new IngestionPipelineSettings { BatchSize = 2, BatchTimeout = TimeSpan.FromSeconds(30) },
            batch =>
            {
                batches.Enqueue(batch.Count);
                Succeed(batch);
            });

        using var cancellation = new CancellationTokenSource(TestTimeout);
        var running = pipeline.Run(cancellation.Token);

        await pipeline.Enqueue(CreateMessage(), cancellation.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(100), cancellation.Token);
        await pipeline.Enqueue(CreateMessage(), cancellation.Token);

        pipeline.CompleteAdding();
        await running;

        Assert.That(batches, Is.EqualTo(new[] { 2 }), "the first message should have waited for the second rather than going on its own");
    }

    [Test]
    public async Task A_partial_batch_is_written_once_the_wait_expires()
    {
        var batches = new ConcurrentQueue<int>();
        var written = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pipeline = Build(
            new IngestionPipelineSettings { BatchSize = 100, BatchTimeout = TimeSpan.FromMilliseconds(50) },
            batch =>
            {
                batches.Enqueue(batch.Count);
                Succeed(batch);
                written.TrySetResult();
            });

        using var cancellation = new CancellationTokenSource(TestTimeout);
        var running = pipeline.Run(cancellation.Token);

        await pipeline.Enqueue(CreateMessage(), cancellation.Token);
        await written.Task.WaitAsync(cancellation.Token);

        pipeline.CompleteAdding();
        await running;

        Assert.That(batches, Is.EqualTo(new[] { 1 }));
    }

    [Test]
    public async Task Writers_hold_batches_at_the_same_time()
    {
        const int writers = 3;

        var arrived = new SemaphoreSlim(0);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pipeline = new IngestionPipeline(
            new IngestionPipelineSettings { BatchSize = 1, MaxWriters = writers },
            async (batch, _) =>
            {
                arrived.Release();
                await release.Task;
                Succeed(batch);
            },
            NullLogger.Instance);

        using var cancellation = new CancellationTokenSource(TestTimeout);
        var running = pipeline.Run(cancellation.Token);

        for (var i = 0; i < writers; i++)
        {
            await pipeline.Enqueue(CreateMessage(), cancellation.Token);
        }

        for (var i = 0; i < writers; i++)
        {
            Assert.That(await arrived.WaitAsync(TestTimeout, cancellation.Token), Is.True, $"only {i} of {writers} writers took a batch");
        }

        release.SetResult();

        pipeline.CompleteAdding();
        await running;
    }

    [Test]
    public async Task A_failed_batch_faults_only_its_own_messages()
    {
        var failure = new InvalidOperationException("storage is down");
        var first = true;
        var pipeline = Build(new IngestionPipelineSettings { BatchSize = 1 }, batch =>
        {
            if (first)
            {
                first = false;
                throw failure;
            }

            Succeed(batch);
        });

        var messages = await Drain(pipeline, 2, TestContext.CurrentContext.CancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(messages[0].GetTaskCompletionSource().Task.Exception?.InnerException, Is.SameAs(failure));
            Assert.That(messages[1].GetTaskCompletionSource().Task.IsCompletedSuccessfully, Is.True, "a failed batch must not take the next one down with it");
        }
    }

    [Test]
    public async Task Messages_that_never_reach_a_writer_are_abandoned_when_the_pipeline_is_cancelled()
    {
        var arrived = new SemaphoreSlim(0);
        var pipeline = new IngestionPipeline(
            new IngestionPipelineSettings { BatchSize = 1 },
            async (_, token) =>
            {
                arrived.Release();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            },
            NullLogger.Instance);

        using var cancellation = new CancellationTokenSource(TestTimeout);
        var running = pipeline.Run(cancellation.Token);

        var messages = new List<MessageContext>();
        for (var i = 0; i < 3; i++)
        {
            var message = CreateMessage();
            messages.Add(message);
            await pipeline.Enqueue(message, cancellation.Token);
        }

        Assert.That(await arrived.WaitAsync(TestTimeout, cancellation.Token), Is.True);

        await cancellation.CancelAsync();
        await running;

        Assert.That(
            messages.Select(message => message.GetTaskCompletionSource().Task.IsCompleted),
            Has.All.True,
            "every message is either written, faulted or abandoned, so nothing is left waiting on a receive that will never be answered");
    }

    static IngestionPipeline Build(IngestionPipelineSettings settings, Action<List<MessageContext>> ingest) =>
        new(settings, (batch, _) =>
        {
            ingest(batch);
            return Task.CompletedTask;
        }, NullLogger.Instance);

    static async Task<List<MessageContext>> Drain(IngestionPipeline pipeline, int messageCount, CancellationToken cancellationToken)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cancellation.CancelAfter(TestTimeout);

        var running = pipeline.Run(cancellation.Token);

        var messages = new List<MessageContext>(messageCount);
        for (var i = 0; i < messageCount; i++)
        {
            var message = CreateMessage();
            messages.Add(message);
            await pipeline.Enqueue(message, cancellation.Token);
        }

        pipeline.CompleteAdding();
        await running;

        return messages;
    }

    static void Succeed(List<MessageContext> batch)
    {
        foreach (var context in batch)
        {
            context.GetTaskCompletionSource().TrySetResult(true);
        }
    }

    static MessageContext CreateMessage()
    {
        var context = new MessageContext(
            Guid.NewGuid().ToString(),
            [],
            ReadOnlyMemory<byte>.Empty,
            new TransportTransaction(),
            "receiveAddress",
            new ContextBag());

        context.SetTaskCompletionSource(new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));

        return context;
    }

    static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(30);
}