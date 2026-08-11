namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Recoverability;

class FailedMessageRetryDataStoreTests : PersistenceTestBase
{
    [Test]
    public async Task RemoveFailedMessageRetry_removes_the_claim_without_touching_the_failed_message()
    {
        var failure = new IngestedFailure { FailingEndpointAddress = "Shipping" };
        var id = await Insert(failure);
        var batchId = await StageBatch(id);

        Assert.That((await RetryStagingStore.GetMessagesToStage(batchId)).Select(m => m.UniqueMessageId), Does.Contain(id));

        await FailedMessageRetryStore.RemoveFailedMessageRetry(id);
        await CompleteDatabaseOperation();

        var failedMessage = await FailedMessageQueryStore.GetFailedMessage(id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await RetryStagingStore.GetMessagesToStage(batchId), Is.Empty, "the retry claim should be removed");
            Assert.That(failedMessage, Is.Not.Null, "the failed message should remain");
            Assert.That(failedMessage.Status, Is.EqualTo(FailedMessageStatus.Unresolved), "the failed-message status should be unchanged");
        }
    }

    [Test]
    public async Task RemoveFailedMessageRetry_is_idempotent_when_no_claim_exists()
    {
        var id = await Insert(new IngestedFailure());

        Assert.DoesNotThrowAsync(() => FailedMessageRetryStore.RemoveFailedMessageRetry(id));
        Assert.That(await FailedMessageQueryStore.GetFailedMessage(id), Is.Not.Null, "the failed message should remain");
    }

    [Test]
    public void RemoveFailedMessageRetry_ignores_a_non_guid_id()
    {
        Assert.DoesNotThrowAsync(() => FailedMessageRetryStore.RemoveFailedMessageRetry("not-a-guid"));
    }

    [Test]
    public async Task GetRetryPendingMessages_returns_only_matching_RetryIssued_messages_as_raw_ids()
    {
        var shipping = await Insert(new IngestedFailure { FailingEndpointAddress = "Shipping" }, FailedMessageStatus.RetryIssued);
        var billing = await Insert(new IngestedFailure { FailingEndpointAddress = "Billing" }, FailedMessageStatus.RetryIssued);
        var unresolved = await Insert(new IngestedFailure { FailingEndpointAddress = "Shipping" }, FailedMessageStatus.Unresolved);
        var resolved = await Insert(new IngestedFailure { FailingEndpointAddress = "Shipping" }, FailedMessageStatus.Resolved);
        var archived = await Insert(new IngestedFailure { FailingEndpointAddress = "Shipping" }, FailedMessageStatus.Archived);

        var ids = await FailedMessageRetryStore.GetRetryPendingMessages(WindowStart, WindowEnd, "Shipping");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ids, Is.EquivalentTo(new[] { shipping }).IgnoreCase);
            Assert.That(ids, Does.Not.Contain(billing).IgnoreCase);
            Assert.That(ids, Does.Not.Contain(unresolved).IgnoreCase);
            Assert.That(ids, Does.Not.Contain(resolved).IgnoreCase);
            Assert.That(ids, Does.Not.Contain(archived).IgnoreCase);
            Assert.That(ids, Has.All.Matches<string>(id => Guid.TryParse(id, out _)), "consumers expect raw unique-message IDs");
        }
    }

    [Test]
    public async Task GetRetryPendingMessages_excludes_messages_outside_the_window()
    {
        await Insert(new IngestedFailure { FailingEndpointAddress = "Shipping" }, FailedMessageStatus.RetryIssued);

        var ids = await FailedMessageRetryStore.GetRetryPendingMessages(DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2), "Shipping");

        Assert.That(ids, Is.Empty);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public async Task GetRetryPendingMessages_with_a_null_or_blank_queue_does_not_match_other_queues(string queueAddress)
    {
        await Insert(new IngestedFailure { FailingEndpointAddress = "Shipping" }, FailedMessageStatus.RetryIssued);
        await Insert(new IngestedFailure { FailingEndpointAddress = "Billing" }, FailedMessageStatus.RetryIssued);

        var ids = await FailedMessageRetryStore.GetRetryPendingMessages(WindowStart, WindowEnd, queueAddress);

        Assert.That(ids, Is.Empty);
    }

    [Test]
    public async Task ProcessPendingRetries_invokes_the_callback_for_each_matching_message()
    {
        var first = await Insert(new IngestedFailure { FailingEndpointAddress = "Shipping" }, FailedMessageStatus.RetryIssued);
        var second = await Insert(new IngestedFailure { FailingEndpointAddress = "Shipping" }, FailedMessageStatus.RetryIssued);
        var billing = await Insert(new IngestedFailure { FailingEndpointAddress = "Billing" }, FailedMessageStatus.RetryIssued);
        var unresolved = await Insert(new IngestedFailure { FailingEndpointAddress = "Shipping" }, FailedMessageStatus.Unresolved);
        var captured = new List<string>();

        await FailedMessageRetryStore.ProcessPendingRetries(WindowStart, WindowEnd, "Shipping", (id, _) =>
        {
            captured.Add(id);
            return Task.CompletedTask;
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(captured, Is.EquivalentTo([first, second]).IgnoreCase);
            Assert.That(captured, Does.Not.Contain(billing).IgnoreCase);
            Assert.That(captured, Does.Not.Contain(unresolved).IgnoreCase);
            Assert.That(captured, Has.All.Matches<string>(id => Guid.TryParse(id, out _)), "callbacks should receive raw unique-message IDs");
        }
    }

    [Test]
    public async Task ProcessPendingRetries_with_a_null_queue_matches_all_queues()
    {
        var shipping = await Insert(new IngestedFailure { FailingEndpointAddress = "Shipping" }, FailedMessageStatus.RetryIssued);
        var billing = await Insert(new IngestedFailure { FailingEndpointAddress = "Billing" }, FailedMessageStatus.RetryIssued);
        var captured = new List<string>();

        await FailedMessageRetryStore.ProcessPendingRetries(WindowStart, WindowEnd, null, (id, _) =>
        {
            captured.Add(id);
            return Task.CompletedTask;
        });

        Assert.That(captured, Is.EquivalentTo(new[] { shipping, billing }).IgnoreCase);
    }

    [Test]
    public void GetFailedMessageBody_throws_for_a_nonexistent_message()
    {
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            FailedMessageRetryStore.GetFailedMessageBody(Guid.NewGuid().ToString()));
    }

    async Task<string> Insert(IngestedFailure failure, FailedMessageStatus status = FailedMessageStatus.Unresolved)
    {
        var message = failure.ToFailedMessage(status);
        message.Id = PersistenceTestsContext.GenerateFailedMessageRecordId(message.UniqueMessageId);

        await PersistenceTestsContext.InsertFailedMessages(message);
        await CompleteDatabaseOperation();

        return failure.UniqueMessageIdString;
    }

    async Task<string> StageBatch(params string[] uniqueMessageIds)
    {
        var batchId = await RetryBatchStore.CreateBatch(
            RetryDocumentManager.RetrySessionId,
            "request-1",
            RetryType.MultipleMessages,
            uniqueMessageIds,
            "a retry request",
            DateTime.UtcNow,
            batchName: "a batch",
            initiatedById: "alice-sub",
            initiatedByName: "Alice",
            operationId: "operation-1");

        await RetryBatchStore.AssignMessagesToBatch(batchId, uniqueMessageIds);
        await RetryBatchStore.MoveBatchToStaging(batchId);
        await CompleteDatabaseOperation();

        return batchId;
    }

    static DateTime WindowStart => DateTime.UtcNow.AddMinutes(-10);
    static DateTime WindowEnd => DateTime.UtcNow.AddMinutes(10);
}
