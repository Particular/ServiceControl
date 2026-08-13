namespace ServiceControl.Persistence.Tests;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contracts.Operations;
using MessageFailures;
using NUnit.Framework;

class EditFailedMessagesDataStoreTests : PersistenceTestBase
{
    [Test]
    public async Task TryBeginEdit_acquires_an_unresolved_message()
    {
        var failedMessage = await CreateUnresolvedFailedMessage();
        var editId = Guid.NewGuid().ToString();

        var result = await EditFailedMessagesStore.TryBeginEdit(failedMessage.UniqueMessageId, editId, TestContext.CurrentContext.CancellationToken);

        var persisted = await FailedMessageQueryStore.GetFailedMessage(failedMessage.UniqueMessageId, TestContext.CurrentContext.CancellationToken);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Outcome, Is.EqualTo(BeginEditOutcome.Acquired));
            Assert.That(result.FailedMessage, Is.Not.Null);
            Assert.That(result.FailedMessage!.UniqueMessageId, Is.EqualTo(failedMessage.UniqueMessageId));
            Assert.That(result.FailedMessage.ProcessingAttempts.Single().MessageId, Is.EqualTo(failedMessage.ProcessingAttempts.Single().MessageId));
            Assert.That(result.ExistingEditId, Is.Null);
            Assert.That(await EditFailedMessagesStore.GetCurrentEditingRequestId(failedMessage.UniqueMessageId), Is.EqualTo(editId));
            Assert.That(persisted!.Status, Is.EqualTo(FailedMessageStatus.Resolved));
        }
    }

    [Test]
    [Repeat(5)]
    public async Task TryBeginEdit_is_idempotent_for_the_same_edit_id()
    {
        var failedMessage = await CreateUnresolvedFailedMessage();
        var editId = Guid.NewGuid().ToString();

        var first = await EditFailedMessagesStore.TryBeginEdit(failedMessage.UniqueMessageId, editId);
        var second = await EditFailedMessagesStore.TryBeginEdit(failedMessage.UniqueMessageId, editId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.Outcome, Is.EqualTo(BeginEditOutcome.Acquired));
            Assert.That(second.Outcome, Is.EqualTo(BeginEditOutcome.Acquired));
            Assert.That(second.FailedMessage, Is.Not.Null, "the dispatch snapshot is required when an edit message is retried");
            Assert.That(second.FailedMessage!.ProcessingAttempts.Single().MessageId, Is.EqualTo(failedMessage.ProcessingAttempts.Single().MessageId));
            Assert.That(second.ExistingEditId, Is.EqualTo(editId));
            Assert.That(await EditFailedMessagesStore.GetCurrentEditingRequestId(failedMessage.UniqueMessageId), Is.EqualTo(editId));
        }
    }

    [Test]
    public async Task TryBeginEdit_reports_the_existing_edit_id_without_mutating_the_message()
    {
        var failedMessage = await CreateUnresolvedFailedMessage();
        var winningEditId = Guid.NewGuid().ToString();
        var losingEditId = Guid.NewGuid().ToString();
        await EditFailedMessagesStore.TryBeginEdit(failedMessage.UniqueMessageId, winningEditId);

        var result = await EditFailedMessagesStore.TryBeginEdit(failedMessage.UniqueMessageId, losingEditId);
        var persisted = await FailedMessageQueryStore.GetFailedMessage(failedMessage.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Outcome, Is.EqualTo(BeginEditOutcome.AcquiredByAnotherEdit));
            Assert.That(result.FailedMessage, Is.Null);
            Assert.That(result.ExistingEditId, Is.EqualTo(winningEditId));
            Assert.That(await EditFailedMessagesStore.GetCurrentEditingRequestId(failedMessage.UniqueMessageId), Is.EqualTo(winningEditId));
            Assert.That(persisted!.Status, Is.EqualTo(FailedMessageStatus.Resolved));
        }
    }

    [Test]
    [Repeat(5)]
    public async Task Concurrent_different_edit_ids_produce_exactly_one_winner()
    {
        var failedMessage = await CreateUnresolvedFailedMessage();
        var editIds = new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() };

        var results = await Task.WhenAll(editIds.Select(editId =>
            EditFailedMessagesStore.TryBeginEdit(failedMessage.UniqueMessageId, editId, TestContext.CurrentContext.CancellationToken)));

        var acquired = results.Single(result => result.Outcome == BeginEditOutcome.Acquired);
        var loser = results.Single(result => result.Outcome == BeginEditOutcome.AcquiredByAnotherEdit);
        var persistedEditId = await EditFailedMessagesStore.GetCurrentEditingRequestId(failedMessage.UniqueMessageId);
        var persisted = await FailedMessageQueryStore.GetFailedMessage(failedMessage.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(acquired.FailedMessage, Is.Not.Null);
            Assert.That(loser.FailedMessage, Is.Null);
            Assert.That(loser.ExistingEditId, Is.EqualTo(persistedEditId));
            Assert.That(editIds, Does.Contain(persistedEditId));
            Assert.That(persisted!.Status, Is.EqualTo(FailedMessageStatus.Resolved));
        }
    }

    [Test]
    public async Task TryBeginEdit_returns_MessageNotFound_for_a_missing_message()
    {
        var failedMessageId = Guid.NewGuid().ToString();

        var result = await EditFailedMessagesStore.TryBeginEdit(failedMessageId, Guid.NewGuid().ToString());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(new BeginEditResult(BeginEditOutcome.MessageNotFound)));
            Assert.That(await EditFailedMessagesStore.GetCurrentEditingRequestId(failedMessageId), Is.Null);
        }
    }

    [TestCase(FailedMessageStatus.Resolved)]
    [TestCase(FailedMessageStatus.RetryIssued)]
    [TestCase(FailedMessageStatus.Archived)]
    public async Task TryBeginEdit_returns_MessageNotUnresolved_without_creating_a_claim(FailedMessageStatus status)
    {
        var failedMessage = await CreateFailedMessage(status);

        var result = await EditFailedMessagesStore.TryBeginEdit(failedMessage.UniqueMessageId, Guid.NewGuid().ToString());
        var persisted = await FailedMessageQueryStore.GetFailedMessage(failedMessage.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(new BeginEditResult(BeginEditOutcome.MessageNotUnresolved)));
            Assert.That(await EditFailedMessagesStore.GetCurrentEditingRequestId(failedMessage.UniqueMessageId), Is.Null);
            Assert.That(persisted!.Status, Is.EqualTo(status));
        }
    }

    [Test]
    public void GetCurrentEditingRequestId_propagates_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.That(
            async () => await EditFailedMessagesStore.GetCurrentEditingRequestId(Guid.NewGuid().ToString(), cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void TryBeginEdit_propagates_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.That(
            async () => await EditFailedMessagesStore.TryBeginEdit(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    Task<FailedMessage> CreateUnresolvedFailedMessage() => CreateFailedMessage(FailedMessageStatus.Unresolved);

    async Task<FailedMessage> CreateFailedMessage(FailedMessageStatus status)
    {
        var failedMessageId = Guid.NewGuid().ToString();
        var attemptedAt = DateTime.UtcNow;
        return await SeedFailedMessage(new FailedMessage
        {
            UniqueMessageId = failedMessageId,
            Status = status,
            ProcessingAttempts =
            [
                new FailedMessage.ProcessingAttempt
                {
                    AttemptedAt = attemptedAt,
                    MessageId = Guid.NewGuid().ToString(),
                    Body = "body",
                    Headers = [],
                    FailureDetails = new FailureDetails
                    {
                        AddressOfFailingEndpoint = "Shipping",
                        TimeOfFailure = attemptedAt
                    }
                }
            ]
        });
    }
}
