namespace ServiceControl.Persistence.Tests.RavenDB.DataMigration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Raven.Client.Documents.Operations.Indexes;
using ServiceControl.MessageFailures;
using ServiceControl.MessageFailures.Api;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.RavenDB;

[TestFixture]
class RetryIssuedFailedMessageSourceTests : RavenMigrationSourceTestBase
{
    static readonly MigrationCategory Category = MigrationCategoryRegistry.Find(MigrationCategoryIds.RecentAndRetriedFailedMessages);

    FailedMessage newestUnresolved;

    // Every message a test seeds is two hours older than this one, so none is recent and the status and claim rules stand alone.
    [SetUp]
    public async Task SeedNewestUnresolved() => newestUnresolved = await SeedFailedMessage(new IngestedFailure().ToFailedMessage(FailedMessageStatus.Unresolved));

    Task<FailedMessage> SeedOld(FailedMessageStatus status) => SeedFailedMessage(new IngestedFailure().ToFailedMessage(status), DateTime.UtcNow.AddHours(-2));

    [Test]
    public async Task Reads_every_retry_issued_message_whether_or_not_its_claim_remains()
    {
        var claimed = await SeedOld(FailedMessageStatus.RetryIssued);
        var released = await SeedOld(FailedMessageStatus.RetryIssued);
        await SeedClaim(claimed);
        await CompleteDatabaseOperation();

        var rows = (await ReadAll()).ToDictionary(row => row.SourceId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rows.Keys, Is.EquivalentTo(new[] { claimed.Id, released.Id }));
            Assert.That(rows[claimed.Id].Metadata[MigrationMetadataKeys.RetryClaimHeld], Is.True);
            Assert.That(rows[released.Id].Metadata[MigrationMetadataKeys.RetryClaimHeld], Is.False);
        }
    }

    [Test]
    public async Task Reads_a_message_that_holds_a_claim_whatever_its_status_and_no_message_without_one()
    {
        var unresolved = await SeedOld(FailedMessageStatus.Unresolved);
        var archived = await SeedOld(FailedMessageStatus.Archived);
        var resolved = await SeedOld(FailedMessageStatus.Resolved);
        await SeedOld(FailedMessageStatus.Unresolved);
        await SeedOld(FailedMessageStatus.Archived);
        await SeedOld(FailedMessageStatus.Resolved);
        await SeedClaim(unresolved);
        await SeedClaim(archived);
        await SeedClaim(resolved);
        await CompleteDatabaseOperation();

        var rows = await ReadAll();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rows.Select(row => row.SourceId), Is.EquivalentTo(new[] { unresolved.Id, archived.Id, resolved.Id }));
            Assert.That(rows.Select(row => row.Metadata[MigrationMetadataKeys.RetryClaimHeld]), Is.All.True, "the claim is copied for a holder of any status, so a retry by id stays blocked");
            Assert.That(rows.Select(row => ((FailedMessage)row.Document).Status), Is.EquivalentTo(new[] { FailedMessageStatus.Unresolved, FailedMessageStatus.Archived, FailedMessageStatus.Resolved }));
        }
    }

    [Test]
    public async Task Reads_an_unresolved_message_without_a_claim_in_neither_category_phase()
    {
        await SeedOld(FailedMessageStatus.Unresolved);
        await CompleteDatabaseOperation();

        Assert.That(await ReadAll(), Is.Empty, "the unresolved copy that runs with the host open owns it");
    }

    [Test]
    public async Task Reads_a_retry_issued_message_with_a_claim_once()
    {
        var message = await SeedOld(FailedMessageStatus.RetryIssued);
        await SeedClaim(message);
        await CompleteDatabaseOperation();

        Assert.That((await ReadAll()).Select(row => row.SourceId), Is.EqualTo(new[] { message.Id }));
    }

    [Test]
    public async Task Skips_a_claim_whose_message_is_gone()
    {
        var gone = new IngestedFailure().ToFailedMessage(FailedMessageStatus.Archived);
        var present = await SeedOld(FailedMessageStatus.Archived);
        await SeedClaim(present);
        await SeedClaim(FailedMessageIdGenerator.MakeDocumentId(gone.UniqueMessageId), gone.UniqueMessageId);
        await CompleteDatabaseOperation();

        Assert.That((await ReadAll()).Select(row => row.SourceId), Is.EqualTo(new[] { present.Id }));
    }

    [Test]
    public async Task The_claim_holders_follow_the_retry_issued_messages_and_their_cursor_resumes_in_that_phase()
    {
        var holders = new List<FailedMessage>();
        var retryIssued = await SeedOld(FailedMessageStatus.RetryIssued);

        for (var count = 0; count < 5; count++)
        {
            var holder = await SeedOld(count % 2 == 0 ? FailedMessageStatus.Archived : FailedMessageStatus.Unresolved);
            await SeedClaim(holder);
            holders.Add(holder);
        }

        await CompleteDatabaseOperation();
        var holderIds = holders.Select(holder => holder.Id).OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray();

        await using var source = await OpenMigrationSource();
        var batches = await CollectBatches(source, Category, batchSize: 2);

        Assert.That(batches.Select(batch => batch.Cursor).Skip(1), Has.All.StartWith("claim-holder:"));

        var resumed = await CollectBatches(source, Category, resumeAfter: batches[1].Cursor, batchSize: 2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batches.SelectMany(batch => batch.Rows).Select(row => row.SourceId).Where(id => id != newestUnresolved.Id), Is.EqualTo(new[] { retryIssued.Id }.Concat(holderIds)), "retry-issued first, then every claim holder once, in id order");
            Assert.That(resumed.SelectMany(batch => batch.Rows).Select(row => row.SourceId), Is.EqualTo(batches.Skip(2).SelectMany(batch => batch.Rows).Select(row => row.SourceId)), "a claim-holder cursor resumes after its message and does not look for it in the retry-issued phase");
        }
    }

    [Test]
    public async Task A_status_phase_cursor_still_copies_older_claim_holders_before_it()
    {
        var holder = await SeedOld(FailedMessageStatus.Archived);
        await SeedClaim(holder);
        var later = await SeedOld(FailedMessageStatus.Unresolved);
        await CompleteDatabaseOperation();
        var cursorPastTheHolder = new[] { holder.Id, later.Id }.Max(StringComparer.Ordinal);

        await using var source = await OpenMigrationSource();
        var rows = (await CollectBatches(source, Category, resumeAfter: cursorPastTheHolder)).SelectMany(batch => batch.Rows).Where(row => row.SourceId != newestUnresolved.Id).ToList();

        Assert.That(rows.Select(row => row.SourceId), Is.EqualTo(new[] { holder.Id }), "a claim holder outside the recent set is read in the claim phase even when its message id precedes the status cursor");
    }

    [Test]
    public async Task Reads_the_claim_holders_when_the_index_is_missing()
    {
        await DocumentStore.Maintenance.SendAsync(new DeleteIndexOperation(new FailedMessageViewIndex().IndexName), TestContext.CurrentContext.CancellationToken);
        var retryIssued = await SeedOld(FailedMessageStatus.RetryIssued);
        var holder = await SeedOld(FailedMessageStatus.Resolved);
        await SeedOld(FailedMessageStatus.Unresolved);
        await SeedClaim(holder);

        var rows = await ReadAll().WaitAsync(TimeSpan.FromSeconds(30), TestContext.CurrentContext.CancellationToken);

        Assert.That(rows.Select(row => row.SourceId), Is.EquivalentTo(new[] { retryIssued.Id, holder.Id }));
    }

    [Test]
    public async Task Counts_the_rows_the_copy_reads()
    {
        var holder = await SeedOld(FailedMessageStatus.Archived);
        await SeedClaim(holder);
        await SeedOld(FailedMessageStatus.RetryIssued);
        await SeedOld(FailedMessageStatus.Unresolved);
        await CompleteDatabaseOperation();

        await using var source = await OpenMigrationSource();

        Assert.That(await source.Count(Category), Is.EqualTo(3), "the holder, the retry-issued message and the newest unresolved message");
    }

    [Test]
    public async Task Carries_the_last_modified_of_a_claim_holder()
    {
        var holder = await SeedOld(FailedMessageStatus.Archived);
        await SeedClaim(holder);
        await CompleteDatabaseOperation();

        var row = (await ReadAll()).Single();

        Assert.That(row.Metadata[MigrationMetadataKeys.LastModified], Is.Not.Null, "the target needs the time the status changed, and a loaded document carries it in its metadata like a streamed one");
    }

    async Task SeedClaim(FailedMessage message) => await SeedClaim(message.Id, message.UniqueMessageId);

    async Task SeedClaim(string failedMessageId, string uniqueMessageId)
    {
        using var session = DocumentStore.OpenAsyncSession();
        await session.StoreAsync(new FailedMessageRetry { FailedMessageId = failedMessageId, RetryBatchId = "RetryBatches/forwarded" }, RetryDocumentDataStore.MakeFailedMessageRetriesDocumentId(uniqueMessageId));
        await session.SaveChangesAsync();
    }

    async Task<List<MigrationRow>> ReadAll()
    {
        await using var source = await OpenMigrationSource();

        return (await CollectBatches(source, Category)).SelectMany(batch => batch.Rows).Where(row => row.SourceId != newestUnresolved.Id).ToList();
    }
}
