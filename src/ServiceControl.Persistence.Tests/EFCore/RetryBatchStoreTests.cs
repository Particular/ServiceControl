namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Recoverability;

class RetryBatchStoreTests : ErrorIngestionTestBase
{
    const string OtherSession = "another-session";

    static readonly DateTime Noon = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Creates_a_batch()
    {
        var batchId = await CreateBatch("request-1", RetryType.FailureGroup, ["OrderPlaced failures"], messageCount: 3);

        var orphaned = await Orphaned();

        var batch = orphaned.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batch.Id, Is.EqualTo(batchId));
            Assert.That(batch.Status, Is.EqualTo(RetryBatchStatus.MarkingDocuments));
            Assert.That(batch.RequestId, Is.EqualTo("request-1"));
            Assert.That(batch.RetryType, Is.EqualTo(RetryType.FailureGroup));
            Assert.That(batch.InitialBatchSize, Is.EqualTo(3));
            Assert.That(batch.Originator, Is.EqualTo("OrderPlaced failures"));
            Assert.That(batch.StartTime, Is.EqualTo(Noon));
        }
    }

    [Test]
    public async Task Does_not_report_batches_of_the_current_session_as_orphaned()
    {
        await CreateBatch("request-1", retrySessionId: OtherSession);

        var orphaned = await RetryBatchStore.GetOrphanedBatches(OtherSession);

        Assert.That(orphaned.Batches, Is.Empty);
    }

    [Test]
    public async Task Does_not_report_staged_batches_as_orphaned()
    {
        var batchId = await CreateBatch("request-1");

        await RetryBatchStore.MoveBatchToStaging(batchId);

        Assert.That(await Orphaned(), Is.Empty);
    }

    [Test]
    public async Task Claims_the_messages_of_a_batch()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var batchId = await CreateBatch("request-1");

        await RetryBatchStore.AssignMessagesToBatch(batchId, [first.ToString(), second.ToString()]);

        var batch = (await Orphaned()).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ClaimedBy(batchId), Is.EquivalentTo(new[] { first.ToString(), second.ToString() }));
            Assert.That(batch.MessageCount, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Leaves_a_message_claimed_by_an_earlier_batch_alone()
    {
        var shared = Guid.NewGuid().ToString();
        var firstBatch = await CreateBatch("request-1");
        var secondBatch = await CreateBatch("request-2");

        await RetryBatchStore.AssignMessagesToBatch(firstBatch, [shared]);
        await RetryBatchStore.AssignMessagesToBatch(secondBatch, [shared]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ClaimedBy(firstBatch), Is.EquivalentTo(new[] { shared }));
            Assert.That(await ClaimedBy(secondBatch), Is.Empty);
        }
    }

    [Test]
    public async Task Claims_each_message_once_when_two_batches_race_for_them()
    {
        var shared = Enumerable.Range(0, 25).Select(_ => Guid.NewGuid().ToString()).ToArray();
        var firstBatch = await CreateBatch("request-1");
        var secondBatch = await CreateBatch("request-2");

        await Task.WhenAll(
            RetryBatchStore.AssignMessagesToBatch(firstBatch, shared),
            RetryBatchStore.AssignMessagesToBatch(secondBatch, shared));

        var claimed = (await ClaimedBy(firstBatch)).Concat(await ClaimedBy(secondBatch));

        Assert.That(claimed, Is.EquivalentTo(shared));
    }

    [Test]
    public async Task Reports_staged_batches_as_available()
    {
        var batchId = await CreateBatch("request-1", RetryType.FailureGroup, ["OrderPlaced failures"], messageCount: 2);

        await RetryBatchStore.MoveBatchToStaging(batchId);

        var group = (await RetryBatchStore.GetAvailableBatchGroups()).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(group.RequestId, Is.EqualTo("request-1"));
            Assert.That(group.RetryType, Is.EqualTo(RetryType.FailureGroup));
            Assert.That(group.HasStagingBatches, Is.True);
            Assert.That(group.HasForwardingBatches, Is.False);
            Assert.That(group.InitialBatchSize, Is.EqualTo(2));
            Assert.That(group.Originator, Is.EqualTo("OrderPlaced failures"));
        }
    }

    [Test]
    public async Task Adds_up_the_batches_of_one_request()
    {
        var first = await CreateBatch("request-1", messageCount: 2);
        var second = await CreateBatch("request-1", messageCount: 5);

        await RetryBatchStore.MoveBatchToStaging(first);
        await RetryBatchStore.MoveBatchToStaging(second);

        var group = (await RetryBatchStore.GetAvailableBatchGroups()).Single();

        Assert.That(group.InitialBatchSize, Is.EqualTo(7));
    }

    [Test]
    public async Task Does_not_report_batches_still_marking_documents_as_available()
    {
        await CreateBatch("request-1");

        Assert.That(await RetryBatchStore.GetAvailableBatchGroups(), Is.Empty);
    }

    [Test]
    public async Task Returns_no_forwarding_batch_when_none_is_in_flight()
    {
        await CreateBatch("request-1");

        Assert.That(await RetryBatchStore.GetCurrentForwardingBatch(), Is.Null);
    }

    [Test]
    public async Task Returns_the_batch_being_forwarded()
    {
        var batchId = await CreateBatch("request-1", RetryType.FailureGroup, ["OrderPlaced failures"]);

        await Store(new RetryBatchNowForwardingEntity { RetryBatchId = Guid.Parse(batchId) });

        var batch = await RetryBatchStore.GetCurrentForwardingBatch();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batch.RequestId, Is.EqualTo("request-1"));
            Assert.That(batch.RetryType, Is.EqualTo(RetryType.FailureGroup));
            Assert.That(batch.Originator, Is.EqualTo("OrderPlaced failures"));
            Assert.That(batch.Classifier, Is.EqualTo("Message Type"));
        }
    }

    [Test]
    public async Task Streams_every_unresolved_message()
    {
        var unresolved = new IngestedFailure();
        await Ingest(unresolved);
        await Insert(new IngestedFailure(), MessageFailures.FailedMessageStatus.Archived);

        var streamed = await Collect(callback => RetryBatchStore.ForEachUnresolvedMessage(callback));

        Assert.That(streamed, Is.EquivalentTo(new[] { unresolved.UniqueMessageIdString }));
    }

    [Test]
    public async Task Streams_the_unresolved_messages_of_an_endpoint()
    {
        var sales = new IngestedFailure { ReceivingEndpoint = new() { Name = "Sales", Host = "H", HostId = Guid.NewGuid() } };
        var shipping = new IngestedFailure { ReceivingEndpoint = new() { Name = "Shipping", Host = "H", HostId = Guid.NewGuid() } };

        await Ingest(sales, shipping);

        var streamed = await Collect(callback => RetryBatchStore.ForEachUnresolvedMessageForEndpoint("Sales", callback));

        Assert.That(streamed, Is.EquivalentTo(new[] { sales.UniqueMessageIdString }));
    }

    [Test]
    public async Task Streams_the_unresolved_messages_of_a_group()
    {
        var group = new MessageFailures.FailedMessage.FailureGroup { Id = Guid.NewGuid().ToString(), Title = "OrderPlaced", Type = "Message Type" };
        var inGroup = new IngestedFailure { Groups = [group] };
        var outsideGroup = new IngestedFailure();

        await Ingest(inGroup, outsideGroup);

        var streamed = await Collect(callback => RetryBatchStore.ForEachUnresolvedMessageInGroup(group.Id, callback));

        Assert.That(streamed, Is.EquivalentTo(new[] { inGroup.UniqueMessageIdString }));
    }

    Task<string> CreateBatch(string requestId, RetryType retryType = RetryType.MultipleMessages, string[] originator = null, int messageCount = 1, string retrySessionId = "this-session") =>
        RetryBatchStore.CreateBatch(
            retrySessionId,
            requestId,
            retryType,
            [.. Enumerable.Range(0, messageCount).Select(_ => Guid.NewGuid().ToString())],
            originator?.Single(),
            Noon,
            classifier: "Message Type");

    async Task<IReadOnlyList<RetryBatch>> Orphaned() => (await RetryBatchStore.GetOrphanedBatches(OtherSession)).Batches;

    async Task<List<string>> ClaimedBy(string batchId)
    {
        var batch = Guid.Parse(batchId);

        var claimed = await Query(dbContext => dbContext.FailedMessageRetries
            .AsNoTracking()
            .Where(retry => retry.RetryBatchId == batch)
            .Select(retry => retry.UniqueMessageId)
            .ToListAsync());

        return [.. claimed.Select(uniqueMessageId => uniqueMessageId.ToString())];
    }

    static async Task<List<string>> Collect(Func<Func<string, DateTime, CancellationToken, Task>, Task> stream)
    {
        var streamed = new List<string>();

        await stream((uniqueMessageId, _, _) =>
        {
            streamed.Add(uniqueMessageId);
            return Task.CompletedTask;
        });

        return streamed;
    }

    async Task Insert(IngestedFailure failure, MessageFailures.FailedMessageStatus status)
    {
        var message = failure.ToFailedMessage(status);
        message.Id = PersistenceTestsContext.GenerateFailedMessageRecordId(message.UniqueMessageId);

        await PersistenceTestsContext.InsertFailedMessages(message);
        await CompleteDatabaseOperation();
    }
}
