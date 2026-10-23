namespace ServiceControl.Persistence.Tests.RavenDB.Recoverability;

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Raven.Client;
using ServiceControl.MessageFailures;

// Ingestion writes failed messages through JavaScript patches. They compare the serialized AttemptedAt
// values as strings, and write the collection and the CLR type of a new document by hand.
[TestFixture]
class RecoverabilityIngestionPatchTests : RavenPersistenceTestBase
{
    [Test]
    public async Task The_first_failure_creates_the_document_in_the_failed_messages_collection()
    {
        var failure = new IngestedFailure();

        await Ingest(failure);

        using var session = DocumentStore.OpenAsyncSession();
        var message = await session.LoadAsync<FailedMessage>(FailedMessageIdGenerator.MakeDocumentId(failure.UniqueMessageIdString));
        var metadata = session.Advanced.GetMetadataFor(message);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(metadata[Constants.Documents.Metadata.Collection], Is.EqualTo("FailedMessages"));
            Assert.That(metadata[Constants.Documents.Metadata.RavenClrType], Is.EqualTo(typeof(FailedMessage).AssemblyQualifiedName));
            Assert.That(message.UniqueMessageId, Is.EqualTo(failure.UniqueMessageIdString));
            Assert.That(message.Status, Is.EqualTo(FailedMessageStatus.Unresolved));
            Assert.That(message.FailureGroups.Select(group => group.Id), Is.EqualTo(failure.Groups.Select(group => group.Id)));
            Assert.That(message.ProcessingAttempts, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task A_redelivered_attempt_is_stored_once()
    {
        var failure = new IngestedFailure();

        await Ingest(failure);
        await Ingest(failure);

        var message = await FailedMessageQueryStore.GetFailedMessage(failure.UniqueMessageIdString);

        Assert.That(message.ProcessingAttempts, Has.Count.EqualTo(1));
    }

    [TestCase(TimeSpan.TicksPerMinute)]
    [TestCase(1)]
    public async Task An_older_attempt_arriving_late_is_stored_in_order_and_leaves_the_status_and_groups_of_the_newest(long newerByTicks)
    {
        var older = new IngestedFailure();
        var newerGroups = new[] { new FailedMessage.FailureGroup { Id = Guid.NewGuid().ToString(), Title = "Newer", Type = "Message Type" } };
        var newer = older.NextAttempt(older.AttemptedAt.AddTicks(newerByTicks), [.. newerGroups]);

        await Ingest(newer);
        await Ingest(older);

        var message = await FailedMessageQueryStore.GetFailedMessage(older.UniqueMessageIdString);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(message.ProcessingAttempts.Select(attempt => attempt.AttemptedAt), Is.EqualTo(new[] { older.AttemptedAt, newer.AttemptedAt }));
            Assert.That(message.FailureGroups.Select(group => group.Id), Is.EqualTo(newerGroups.Select(group => group.Id)));
            Assert.That(message.Status, Is.EqualTo(FailedMessageStatus.Unresolved));
        }
    }

    [TestCase(0, FailedMessageStatus.Resolved)]
    [TestCase(-1, FailedMessageStatus.Unresolved)]
    public async Task A_retry_resolves_the_message_only_when_it_succeeded_at_or_after_the_newest_attempt(long succeededAfterAttemptByTicks, FailedMessageStatus expected)
    {
        var failure = new IngestedFailure();
        await Ingest(failure);

        await using (var unitOfWork = await UnitOfWorkFactory.StartNew())
        {
            await unitOfWork.Recoverability.RecordSuccessfulRetry(failure.UniqueMessageIdString, failure.AttemptedAt.AddTicks(succeededAfterAttemptByTicks));
            await unitOfWork.Complete(TestContext.CurrentContext.CancellationToken);
        }

        await CompleteDatabaseOperation();

        var message = await FailedMessageQueryStore.GetFailedMessage(failure.UniqueMessageIdString);

        Assert.That(message.Status, Is.EqualTo(expected));
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
}
