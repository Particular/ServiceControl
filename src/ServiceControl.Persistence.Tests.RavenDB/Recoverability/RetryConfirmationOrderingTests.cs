namespace ServiceControl.Persistence.Tests.RavenDB.Recoverability;

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.MessageFailures;

// A retry acknowledgement and a later failure of the same message reach storage in whatever order
// their batches commit. Both orders are forced here rather than raced, so the assertion is about
// the end state and not about which batch got there first.
[TestFixture]
class RetryConfirmationOrderingTests : RavenPersistenceTestBase
{
    [Test]
    public async Task A_confirmation_arriving_after_a_later_attempt_leaves_the_message_unresolved()
    {
        var failure = new IngestedFailure();
        var retrySucceededAt = failure.AttemptedAt.AddMinutes(1);

        await Ingest(failure);
        await Ingest(failure.NextAttempt(retrySucceededAt.AddMinutes(1)));
        await ConfirmRetry(failure.UniqueMessageIdString, retrySucceededAt);

        var message = await FailedMessageQueryStore.GetFailedMessage(failure.UniqueMessageIdString);

        Assert.That(message.Status, Is.EqualTo(FailedMessageStatus.Unresolved), "the message failed again after the retry succeeded");
    }

    [Test]
    public async Task A_later_attempt_arriving_after_a_confirmation_leaves_the_message_unresolved()
    {
        var failure = new IngestedFailure();
        var retrySucceededAt = failure.AttemptedAt.AddMinutes(1);

        await Ingest(failure);
        await ConfirmRetry(failure.UniqueMessageIdString, retrySucceededAt);
        await Ingest(failure.NextAttempt(retrySucceededAt.AddMinutes(1)));

        var message = await FailedMessageQueryStore.GetFailedMessage(failure.UniqueMessageIdString);

        Assert.That(message.Status, Is.EqualTo(FailedMessageStatus.Unresolved), "the message failed again after the retry succeeded");
    }

    [Test]
    public async Task A_redelivered_attempt_arriving_after_a_confirmation_leaves_the_message_resolved()
    {
        var failure = new IngestedFailure();

        await Ingest(failure);
        await ConfirmRetry(failure.UniqueMessageIdString, failure.AttemptedAt.AddMinutes(1));
        await Ingest(failure);

        var message = await FailedMessageQueryStore.GetFailedMessage(failure.UniqueMessageIdString);

        Assert.That(message.Status, Is.EqualTo(FailedMessageStatus.Resolved), "redelivering the attempt the retry was for is not a new failure");
    }

    async Task Ingest(IngestedFailure failure)
    {
        await using (var unitOfWork = await UnitOfWorkFactory.StartNew())
        {
            await unitOfWork.Recoverability.RecordFailedProcessingAttempt(failure.Context, failure.ProcessingAttempt, failure.Groups);
            await unitOfWork.Complete(TestContext.CurrentContext.CancellationToken);
        }

        await CompleteDatabaseOperation();
    }

    async Task ConfirmRetry(string uniqueMessageId, DateTime succeededAt)
    {
        await using (var unitOfWork = await UnitOfWorkFactory.StartNew())
        {
            await unitOfWork.Recoverability.RecordSuccessfulRetry(uniqueMessageId, succeededAt);
            await unitOfWork.Complete(TestContext.CurrentContext.CancellationToken);
        }

        await CompleteDatabaseOperation();
    }
}