namespace ServiceControl.Persistence.Tests;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Recoverability;

class RetryStagingStoreTests : PersistenceTestBase
{
    [Test]
    public async Task Returns_no_staging_batch_when_none_is_staged()
    {
        var failure = await Insert(new IngestedFailure());

        await CreateBatch(failure);

        Assert.That(await RetryStagingStore.GetStagingBatch(), Is.Null);
    }

    [Test]
    public async Task Returns_the_staged_batch()
    {
        var failure = await Insert(new IngestedFailure());

        var batchId = await StageBatch(failure);

        var batch = await RetryStagingStore.GetStagingBatch();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batch.Id, Is.EqualTo(batchId));
            Assert.That(batch.Status, Is.EqualTo(RetryBatchStatus.Staging));
            Assert.That(batch.RequestId, Is.EqualTo(RequestId));
            Assert.That(batch.RetryType, Is.EqualTo(RetryType.MultipleMessages));
            Assert.That(batch.InitialBatchSize, Is.EqualTo(1));
            Assert.That(batch.Originator, Is.EqualTo("a retry request"));
            Assert.That(batch.Context, Is.EqualTo("a batch"));
            Assert.That(batch.OperationId, Is.EqualTo("operation-1"));
            Assert.That(batch.InitiatedById, Is.EqualTo("alice-sub"));
            Assert.That(batch.InitiatedByName, Is.EqualTo("Alice"));
        }
    }

    [Test]
    public async Task Returns_the_messages_of_the_batch()
    {
        var first = await Insert(new IngestedFailure());
        var second = await Insert(new IngestedFailure());

        var batchId = await StageBatch(first, second);

        var messages = await RetryStagingStore.GetMessagesToStage(batchId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(messages.Select(message => message.UniqueMessageId), Is.EquivalentTo(new[] { first, second }));
            Assert.That(messages.Select(message => message.StageAttempts), Is.All.Zero);
        }
    }

    [Test]
    public async Task Skips_the_messages_an_earlier_batch_claimed()
    {
        var failure = await Insert(new IngestedFailure());

        await CreateBatch(failure);
        var second = await StageBatch(failure);

        Assert.That(await RetryStagingStore.GetMessagesToStage(second), Is.Empty);
    }

    [Test]
    public async Task Skips_the_messages_that_are_gone()
    {
        var failure = await Insert(new IngestedFailure());

        var batchId = await StageBatch(failure, Guid.NewGuid().ToString());

        var messages = await RetryStagingStore.GetMessagesToStage(batchId);

        Assert.That(messages.Single().UniqueMessageId, Is.EqualTo(failure));
    }

    [Test]
    public async Task Marking_as_forwarding_issues_the_retry_and_hands_the_batch_to_the_forwarder()
    {
        var failure = await Insert(new IngestedFailure());

        var batchId = await StageBatch(failure);

        await RetryStagingStore.MarkBatchAsForwarding(batchId, "staging-1", [failure]);
        await CompleteDatabaseOperation();

        var batch = await RetryStagingStore.GetBatch(batchId, CancellationToken.None);
        var message = await FailedMessageQueryStore.GetFailedMessage(failure);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await RetryStagingStore.GetForwardingBatchId(), Is.EqualTo(batchId));
            Assert.That(batch.Status, Is.EqualTo(RetryBatchStatus.Forwarding));
            Assert.That(batch.StagingId, Is.EqualTo("staging-1"));
            Assert.That(batch.MessageCount, Is.EqualTo(1));
            Assert.That(message.Status, Is.EqualTo(FailedMessageStatus.RetryIssued));
        }
    }

    [Test]
    public async Task Marking_as_forwarding_leaves_out_the_messages_that_were_not_staged()
    {
        var staged = await Insert(new IngestedFailure());
        var notStaged = await Insert(new IngestedFailure());

        var batchId = await StageBatch(staged, notStaged);

        await RetryStagingStore.MarkBatchAsForwarding(batchId, "staging-1", [staged]);
        await CompleteDatabaseOperation();

        var batch = await RetryStagingStore.GetBatch(batchId, CancellationToken.None);
        var message = await FailedMessageQueryStore.GetFailedMessage(notStaged);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batch.MessageCount, Is.EqualTo(1));
            Assert.That(message.Status, Is.EqualTo(FailedMessageStatus.Unresolved));
        }
    }

    [Test]
    public async Task Completing_forwarding_removes_the_batch_and_the_pointer()
    {
        var failure = await Insert(new IngestedFailure());

        var batchId = await StageBatch(failure);

        await RetryStagingStore.MarkBatchAsForwarding(batchId, "staging-1", [failure]);
        await RetryStagingStore.CompleteForwarding(batchId);
        await CompleteDatabaseOperation();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await RetryStagingStore.GetForwardingBatchId(), Is.Null);
            Assert.That(await RetryStagingStore.GetBatch(batchId, CancellationToken.None), Is.Null);
        }
    }

    [Test]
    public async Task Completing_forwarding_clears_a_pointer_to_a_batch_that_is_gone()
    {
        var failure = await Insert(new IngestedFailure());

        var batchId = await StageBatch(failure);

        await RetryStagingStore.MarkBatchAsForwarding(batchId, "staging-1", [failure]);
        await RetryStagingStore.DiscardBatch(batchId);
        await CompleteDatabaseOperation();

        Assert.That(await RetryStagingStore.GetForwardingBatchId(), Is.EqualTo(batchId), "the pointer outlives the batch it points at");

        await RetryStagingStore.CompleteForwarding(batchId);
        await CompleteDatabaseOperation();

        Assert.That(await RetryStagingStore.GetForwardingBatchId(), Is.Null);
    }

    [Test]
    public async Task Discarding_a_batch_takes_it_out_of_staging()
    {
        var failure = await Insert(new IngestedFailure());

        var batchId = await StageBatch(failure);

        await RetryStagingStore.DiscardBatch(batchId);
        await CompleteDatabaseOperation();

        Assert.That(await RetryStagingStore.GetStagingBatch(), Is.Null);
    }

    [Test]
    public async Task Recording_a_staging_failure_counts_an_attempt()
    {
        var failure = await Insert(new IngestedFailure());

        var batchId = await StageBatch(failure);

        await RetryStagingStore.RecordStagingFailure([failure]);
        await CompleteDatabaseOperation();

        var messages = await RetryStagingStore.GetMessagesToStage(batchId);

        Assert.That(messages.Single().StageAttempts, Is.EqualTo(1));
    }

    [Test]
    public async Task Incrementing_the_staging_attempts_counts_another_attempt()
    {
        var failure = await Insert(new IngestedFailure());

        var batchId = await StageBatch(failure);

        await RetryStagingStore.RecordStagingFailure([failure]);
        await RetryStagingStore.IncrementStagingAttempts(failure);
        await CompleteDatabaseOperation();

        var messages = await RetryStagingStore.GetMessagesToStage(batchId);

        Assert.That(messages.Single().StageAttempts, Is.EqualTo(2));
    }

    [Test]
    public async Task Removing_a_message_from_the_batch_leaves_it_out_of_staging()
    {
        var removed = await Insert(new IngestedFailure());
        var kept = await Insert(new IngestedFailure());

        var batchId = await StageBatch(removed, kept);

        await RetryStagingStore.RemoveFromBatch(removed);
        await CompleteDatabaseOperation();

        var messages = await RetryStagingStore.GetMessagesToStage(batchId);

        Assert.That(messages.Single().UniqueMessageId, Is.EqualTo(kept));
    }

    async Task<string> Insert(IngestedFailure failure)
    {
        var message = failure.ToFailedMessage();
        message.Id = PersistenceTestsContext.GenerateFailedMessageRecordId(message.UniqueMessageId);

        await PersistenceTestsContext.InsertFailedMessages(message);
        await CompleteDatabaseOperation();

        return failure.UniqueMessageIdString;
    }

    async Task<string> CreateBatch(params string[] uniqueMessageIds)
    {
        var batchId = await RetryBatchStore.CreateBatch(
            RetryDocumentManager.RetrySessionId,
            RequestId,
            RetryType.MultipleMessages,
            uniqueMessageIds,
            "a retry request",
            DateTime.UtcNow,
            batchName: "a batch",
            initiatedById: "alice-sub",
            initiatedByName: "Alice",
            operationId: "operation-1");

        await RetryBatchStore.AssignMessagesToBatch(batchId, uniqueMessageIds);
        await CompleteDatabaseOperation();

        return batchId;
    }

    async Task<string> StageBatch(params string[] uniqueMessageIds)
    {
        var batchId = await CreateBatch(uniqueMessageIds);

        await RetryBatchStore.MoveBatchToStaging(batchId);
        await CompleteDatabaseOperation();

        return batchId;
    }

    const string RequestId = "request-1";
}
