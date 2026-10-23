namespace ServiceControl.Persistence.Tests.RavenDB.Expiration;

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Raven.Client;
using Raven.Client.ServerWide.Operations;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.RavenDB;
using ServiceControl.Recoverability;

// The server deletes a document once its @expires value has passed. These tests check where ServiceControl
// puts the value on and takes it off, because the JavaScript patches that do it fail silently when they break.
[TestFixture]
class ExpiresMetadataTests : RavenPersistenceTestBase
{
    [Test]
    public async Task Database_setup_configures_the_server_to_delete_expired_documents()
    {
        var settings = (RavenPersisterSettings)PersistenceSettings;

        var databaseRecord = await DocumentStore.Maintenance.Server.SendAsync(new GetDatabaseRecordOperation(settings.DatabaseName));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(databaseRecord.Expiration.Disabled, Is.False);
            Assert.That(databaseRecord.Expiration.DeleteFrequencyInSec, Is.EqualTo(settings.ExpirationProcessTimerInSeconds));
        }
    }

    [Test]
    public async Task Archiving_a_group_sets_the_expiry_and_unarchiving_it_removes_it()
    {
        var group = new FailedMessage.FailureGroup { Id = Guid.NewGuid().ToString(), Title = "OrderPlaced", Type = "Message Type" };
        var failure = new IngestedFailure { Groups = [group] };
        var documentId = await Seed(failure, FailedMessageStatus.Unresolved, expires: false);

        await ArchiveMessages.ArchiveAllInGroup(group.Id);
        var archived = await Load(documentId);

        await CompleteDatabaseOperation();
        await ArchiveMessages.UnarchiveAllInGroup(group.Id);
        var unarchived = await Load(documentId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(archived.Status, Is.EqualTo(FailedMessageStatus.Archived));
            Assert.That(archived.HasExpiry, Is.True);
            Assert.That(unarchived.Status, Is.EqualTo(FailedMessageStatus.Unresolved));
            Assert.That(unarchived.HasExpiry, Is.False);
        }
    }

    [Test]
    public async Task Unarchiving_messages_by_id_removes_the_expiry()
    {
        var failure = new IngestedFailure();
        var documentId = await Seed(failure, FailedMessageStatus.Archived, expires: true);

        await FailedMessageLifecycleStore.UnArchiveMessages([failure.UniqueMessageIdString]);

        var message = await Load(documentId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(message.Status, Is.EqualTo(FailedMessageStatus.Unresolved));
            Assert.That(message.HasExpiry, Is.False);
        }
    }

    [Test]
    public async Task Unarchiving_messages_by_range_unarchives_only_the_messages_in_the_range_and_removes_their_expiry()
    {
        var failure = new IngestedFailure();
        var documentId = await Seed(failure, FailedMessageStatus.Archived, expires: true);
        var now = DateTime.UtcNow;

        var outsideRange = await FailedMessageLifecycleStore.UnArchiveMessagesByRange(now.AddHours(1), now.AddHours(2));
        var untouched = await Load(documentId);

        var insideRange = await FailedMessageLifecycleStore.UnArchiveMessagesByRange(now.AddHours(-1), now.AddHours(1));
        var unarchived = await Load(documentId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outsideRange, Is.Empty);
            Assert.That(untouched.Status, Is.EqualTo(FailedMessageStatus.Archived));
            Assert.That(untouched.HasExpiry, Is.True);
            Assert.That(insideRange, Is.EqualTo(new[] { documentId }));
            Assert.That(unarchived.Status, Is.EqualTo(FailedMessageStatus.Unresolved));
            Assert.That(unarchived.HasExpiry, Is.False);
        }
    }

    [Test]
    public async Task Issuing_a_retry_removes_the_expiry()
    {
        var failure = new IngestedFailure();
        var documentId = await Seed(failure, FailedMessageStatus.Unresolved, expires: true);

        var batchId = await RetryBatchStore.CreateBatch(
            RetryDocumentManager.RetrySessionId,
            "request-1",
            RetryType.MultipleMessages,
            [failure.UniqueMessageIdString],
            "a retry request",
            DateTime.UtcNow);
        await RetryBatchStore.AssignMessagesToBatch(batchId, [failure.UniqueMessageIdString]);
        await RetryBatchStore.MoveBatchToStaging(batchId);

        await RetryStagingStore.MarkBatchAsForwarding(batchId, "staging-1", [failure.UniqueMessageIdString]);

        var message = await Load(documentId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(message.Status, Is.EqualTo(FailedMessageStatus.RetryIssued));
            Assert.That(message.HasExpiry, Is.False);
        }
    }

    async Task<string> Seed(IngestedFailure failure, FailedMessageStatus status, bool expires)
    {
        var message = failure.ToFailedMessage(status);
        message.Id = PersistenceTestsContext.GenerateFailedMessageRecordId(message.UniqueMessageId);

        using (var session = await SessionProvider.OpenSession())
        {
            await session.StoreAsync(message);

            if (expires)
            {
                session.Advanced.GetMetadataFor(message)[Constants.Documents.Metadata.Expires] = DateTime.UtcNow.AddDays(30);
            }

            await session.SaveChangesAsync();
        }

        await CompleteDatabaseOperation();

        return message.Id;
    }

    async Task<(FailedMessageStatus Status, bool HasExpiry)> Load(string documentId)
    {
        using var session = DocumentStore.OpenAsyncSession();
        var message = await session.LoadAsync<FailedMessage>(documentId);

        return (message.Status, session.Advanced.GetMetadataFor(message).ContainsKey(Constants.Documents.Metadata.Expires));
    }
}
