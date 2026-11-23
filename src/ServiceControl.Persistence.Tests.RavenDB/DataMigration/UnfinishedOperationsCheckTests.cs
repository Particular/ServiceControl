namespace ServiceControl.Persistence.Tests.RavenDB.DataMigration;

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.RavenDB;
using ServiceControl.Persistence.RavenDB.Recoverability;
using ServiceControl.Recoverability;

class UnfinishedOperationsCheckTests : RavenMigrationSourceTestBase
{
    [Test]
    public async Task The_source_contributes_the_check()
    {
        await using var source = await OpenMigrationSource();

        Assert.That(source.ContributedChecks(MigrationCategoryKind.Required).Select(check => check.Name), Has.Member("RavenDB holds no unfinished retry, archive or unarchive operations"));
    }

    [Test]
    public async Task The_source_contributes_no_check_to_the_online_copy()
    {
        await Seed("retry batch");
        await using var source = await OpenMigrationSource();

        Assert.That(source.ContributedChecks(MigrationCategoryKind.Optional), Is.Empty, "a refusal after ServiceControl opened on SQL would advise going back to RavenDB");
    }

    [Test]
    public async Task Passes_when_no_operation_is_persisted()
    {
        await using var source = await OpenMigrationSource();

        Assert.DoesNotThrowAsync(() => RunChecks(source));
    }

    [Test]
    public async Task Passes_when_only_completed_retry_history_is_persisted()
    {
        await RetryHistoryStore.RecordRetryOperationCompleted("group-1", RetryType.FailureGroup, DateTime.UtcNow, DateTime.UtcNow, "Sales.Orders", "Exception Type", false, 1, DateTime.UtcNow, 10);
        await CompleteDatabaseOperation();

        await using var source = await OpenMigrationSource();

        Assert.DoesNotThrowAsync(() => RunChecks(source));
    }

    [Test]
    public async Task Passes_when_only_a_retry_claim_is_persisted()
    {
        var messageId = Guid.NewGuid().ToString();
        var claimId = RetryDocumentDataStore.MakeFailedMessageRetriesDocumentId(messageId);
        using (var session = DocumentStore.OpenAsyncSession(new Raven.Client.Documents.Session.SessionOptions { Database = ((RavenPersisterSettings)PersistenceSettings).DatabaseName }))
        {
            await session.StoreAsync(new FailedMessageRetry
            {
                Id = claimId,
                FailedMessageId = messageId,
                RetryBatchId = RetryDocumentDataStore.MakeDocumentId("completed-batch")
            }, claimId);
            await session.SaveChangesAsync();
        }

        await using var source = await OpenMigrationSource();

        Assert.DoesNotThrowAsync(() => RunChecks(source));
    }

    [TestCase("retry batch")]
    [TestCase("forwarding marker")]
    [TestCase("archive operation")]
    [TestCase("archive batch")]
    [TestCase("unarchive operation")]
    [TestCase("unarchive batch")]
    public async Task Refuses_when_an_unfinished_operation_is_persisted_and_says_how_to_finish_it(string unfinished)
    {
        var documentId = await Seed(unfinished);
        await using var source = await OpenMigrationSource();

        var refusal = Assert.ThrowsAsync<InvalidOperationException>(() => RunChecks(source));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refusal.Message, Does.Contain(documentId), "the operator is told which document is unfinished");
            Assert.That(refusal.Message, Does.Contain("Start ServiceControl on RavenDB again"));
            Assert.That(refusal.Message, Does.Contain("--start-data-migration again"));
            Assert.That(refusal.Message, Does.Contain("requested again on RavenDB"));
            Assert.That(refusal.Message, Does.Contain("contact Particular support"));
            Assert.That(refusal.Message, Does.Not.Contain("delete"));
        }
    }

    [Test]
    public async Task Names_every_kind_of_unfinished_operation()
    {
        await Seed("retry batch");
        await Seed("archive operation");
        await Seed("unarchive operation");
        await using var source = await OpenMigrationSource();

        var refusal = Assert.ThrowsAsync<InvalidOperationException>(() => RunChecks(source));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refusal.Message, Does.Contain("retry batches"));
            Assert.That(refusal.Message, Does.Contain("archive operations"));
            Assert.That(refusal.Message, Does.Contain("unarchive operations"));
        }
    }

    static async Task RunChecks(Persistence.DataMigration.IMigrationSource source)
    {
        foreach (var check in source.ContributedChecks(MigrationCategoryKind.Required))
        {
            await check.Run();
        }
    }

    async Task<string> Seed(string unfinished)
    {
        using var session = DocumentStore.OpenAsyncSession(new Raven.Client.Documents.Session.SessionOptions { Database = ((RavenPersisterSettings)PersistenceSettings).DatabaseName });

        string id;

        switch (unfinished)
        {
            case "retry batch":
                id = RetryDocumentDataStore.MakeDocumentId(Guid.NewGuid().ToString());
                await session.StoreAsync(new RetryBatch { Id = id, Status = RetryBatchStatus.Forwarding }, id);
                break;
            case "forwarding marker":
                id = RetryDocumentDataStore.NowForwardingDocumentId;
                await session.StoreAsync(new RetryBatchNowForwarding { RetryBatchId = RetryDocumentDataStore.MakeDocumentId("batch-1") }, id);
                break;
            case "archive operation":
                id = ArchiveOperation.MakeId("group-1", ArchiveType.FailureGroup);
                await session.StoreAsync(new ArchiveOperation { Id = id, RequestId = "group-1", ArchiveType = ArchiveType.FailureGroup }, id);
                break;
            case "archive batch":
                id = ArchiveBatch.MakeId("group-1", ArchiveType.FailureGroup, 1);
                await session.StoreAsync(new ArchiveBatch { Id = id }, id);
                break;
            case "unarchive operation":
                id = UnarchiveOperation.MakeId("group-1", ArchiveType.FailureGroup);
                await session.StoreAsync(new UnarchiveOperation { Id = id, RequestId = "group-1", ArchiveType = ArchiveType.FailureGroup }, id);
                break;
            case "unarchive batch":
                id = UnarchiveBatch.MakeId("group-1", ArchiveType.FailureGroup, 1);
                await session.StoreAsync(new UnarchiveBatch { Id = id }, id);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(unfinished), unfinished, null);
        }

        await session.SaveChangesAsync();
        await CompleteDatabaseOperation();

        return id;
    }
}
