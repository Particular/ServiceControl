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

/// <summary>
/// The required failed message category reads the retry-issued messages, the claim holders and the recent errors, which are the
/// messages of any status last modified at or after the newest unresolved message's last-modified time less a window.
/// Each test runs through the index and through the full-collection read, which must read the same rows.
/// </summary>
[TestFixture]
class RecentErrorsSourceTests : RavenMigrationSourceTestBase
{
    static readonly MigrationCategory Category = MigrationCategoryRegistry.Find(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);
    static readonly FailedMessageStatus[] AllStatuses = [FailedMessageStatus.Unresolved, FailedMessageStatus.Archived, FailedMessageStatus.Resolved, FailedMessageStatus.RetryIssued];

    [Test]
    public async Task Reads_every_status_modified_at_or_after_the_cutoff_and_only_the_retry_issued_before_it([Values] bool throughIndex)
    {
        await UseReadPath(throughIndex);
        var newestUnresolved = await Seed(FailedMessageStatus.Unresolved);
        await CompleteDatabaseOperation();
        var anchor = await LastModified(newestUnresolved);
        var cutoff = anchor - TimeSpan.FromHours(1);

        var before = new List<FailedMessage>();
        foreach (var status in AllStatuses)
        {
            before.Add(await Seed(status, cutoff - TimeSpan.FromHours(1)));
        }

        var tickBefore = await Seed(FailedMessageStatus.Archived, cutoff - TimeSpan.FromTicks(1));
        var boundary = await Seed(FailedMessageStatus.Archived, cutoff);
        var between = new List<FailedMessage>();
        var afterAnchor = new List<FailedMessage>();
        foreach (var status in AllStatuses)
        {
            between.Add(await Seed(status, anchor - TimeSpan.FromMinutes(30)));

            // A later unresolved message would move the anchor.
            if (status != FailedMessageStatus.Unresolved)
            {
                afterAnchor.Add(await Seed(status, anchor + TimeSpan.FromMinutes(30)));
            }
        }

        await CompleteDatabaseOperation();

        var oldRetryIssued = before.Single(message => message.Status == FailedMessageStatus.RetryIssued);
        var expected = between.Concat(afterAnchor).Concat([boundary, newestUnresolved, oldRetryIssued]).Select(message => message.Id);

        var rows = await ReadAll();

        Assert.That(rows.Select(row => row.SourceId), Is.EquivalentTo(expected), "a message modified at the cutoff or later is recent whatever its status, with no upper bound, and one a tick before it is read only when retry-issued");
        Assert.That(rows.Select(row => row.SourceId), Does.Not.Contain(tickBefore.Id));
    }

    [Test]
    public async Task Reads_no_recent_errors_when_there_is_no_unresolved_message([Values] bool throughIndex)
    {
        await UseReadPath(throughIndex);
        var retryIssued = await Seed(FailedMessageStatus.RetryIssued);
        var claimedArchived = await Seed(FailedMessageStatus.Archived);
        var claimedResolved = await Seed(FailedMessageStatus.Resolved);
        await Seed(FailedMessageStatus.Archived);
        await Seed(FailedMessageStatus.Resolved);
        await SeedClaim(claimedArchived);
        await SeedClaim(claimedResolved);
        await CompleteDatabaseOperation();

        var rows = await ReadAll();

        Assert.That(rows.Select(row => row.SourceId), Is.EquivalentTo(new[] { retryIssued.Id, claimedArchived.Id, claimedResolved.Id }), "every retry-issued message and claim holder is read, and the recent hour is not expanded without an anchor");
    }

    [Test]
    public async Task Reads_each_message_once_with_the_claim_it_holds([Values] bool throughIndex)
    {
        await UseReadPath(throughIndex);
        var oldRetryIssuedClaimed = await Seed(FailedMessageStatus.RetryIssued, TwoHoursAgo);
        var oldRetryIssuedReleased = await Seed(FailedMessageStatus.RetryIssued, TwoHoursAgo);
        var oldUnresolvedClaimed = await Seed(FailedMessageStatus.Unresolved, TwoHoursAgo);
        var oldArchivedClaimed = await Seed(FailedMessageStatus.Archived, TwoHoursAgo);
        var oldResolvedClaimed = await Seed(FailedMessageStatus.Resolved, TwoHoursAgo);
        var oldArchivedPlain = await Seed(FailedMessageStatus.Archived, TwoHoursAgo);
        var oldUnresolvedPlain = await Seed(FailedMessageStatus.Unresolved, TwoHoursAgo);
        var cutoffMessage = await Seed(FailedMessageStatus.Resolved);
        var recentRetryIssuedClaimed = await Seed(FailedMessageStatus.RetryIssued);
        var recentUnresolvedClaimed = await Seed(FailedMessageStatus.Unresolved);
        var recentArchivedClaimed = await Seed(FailedMessageStatus.Archived);
        var recentResolvedPlain = await Seed(FailedMessageStatus.Resolved);
        var newestUnresolved = await Seed(FailedMessageStatus.Unresolved);

        foreach (var claimed in new[] { oldRetryIssuedClaimed, oldUnresolvedClaimed, oldArchivedClaimed, oldResolvedClaimed, recentRetryIssuedClaimed, recentUnresolvedClaimed, recentArchivedClaimed })
        {
            await SeedClaim(claimed);
        }

        await CompleteDatabaseOperation();

        var rows = await ReadAll();
        var claimHeld = rows.ToDictionary(row => row.SourceId, row => (bool)row.Metadata[MigrationMetadataKeys.RetryClaimHeld]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rows, Has.Count.EqualTo(rows.Select(row => row.SourceId).Distinct().Count()), "an overlap between the status phase and the claim phase would read a message twice");
            Assert.That(claimHeld.Keys, Is.EquivalentTo(new[]
            {
                oldRetryIssuedClaimed, oldRetryIssuedReleased, oldUnresolvedClaimed, oldArchivedClaimed, oldResolvedClaimed,
                cutoffMessage, recentRetryIssuedClaimed, recentUnresolvedClaimed, recentArchivedClaimed, recentResolvedPlain, newestUnresolved
            }.Select(message => message.Id)));
            Assert.That(claimHeld.Where(pair => pair.Value).Select(pair => pair.Key), Is.EquivalentTo(new[]
            {
                oldRetryIssuedClaimed, oldUnresolvedClaimed, oldArchivedClaimed, oldResolvedClaimed, recentRetryIssuedClaimed, recentUnresolvedClaimed, recentArchivedClaimed
            }.Select(message => message.Id)), "every message that holds a claim says so, whatever its status, age or phase, and the others say they hold none");
            Assert.That(claimHeld.ContainsKey(oldArchivedPlain.Id) || claimHeld.ContainsKey(oldUnresolvedPlain.Id), Is.False, "an older message without a claim waits for the optional copy");
            Assert.That(await CountThrough(), Is.EqualTo(rows.Count), "the count reads the same set the copy does");
        }
    }

    [Test]
    public async Task A_resume_from_any_batch_cursor_reads_the_rest_without_a_gap_or_a_repeat([Values] bool throughIndex)
    {
        await UseReadPath(throughIndex);
        var seeded = new List<FailedMessage>();
        for (var count = 0; count < 4; count++)
        {
            seeded.Add(await Seed(AllStatuses[count % AllStatuses.Length], TwoHoursAgo));
        }

        var cutoffMessage = await Seed(FailedMessageStatus.Unresolved);
        for (var count = 0; count < 9; count++)
        {
            var message = await Seed(AllStatuses[count % AllStatuses.Length]);
            if (count % 3 == 0)
            {
                await SeedClaim(message);
            }
        }

        var newestUnresolved = await Seed(FailedMessageStatus.Unresolved);

        foreach (var oldMessage in seeded.Where(message => message.Status != FailedMessageStatus.RetryIssued))
        {
            await SeedClaim(oldMessage);
        }

        await CompleteDatabaseOperation();

        await using var source = await OpenMigrationSource();
        var batches = await CollectBatches(source, Category, batchSize: 2);
        var all = batches.SelectMany(batch => batch.Rows).Select(row => row.SourceId).ToArray();

        Assert.That(batches.Select(batch => batch.Cursor), Has.Some.StartWith("claim-holder:"), "the set must cross the phase boundary for the test to prove anything");
        Assert.That(all, Is.Unique);

        for (var resumeAt = 0; resumeAt < batches.Count; resumeAt++)
        {
            var resumed = await CollectBatches(source, Category, resumeAfter: batches[resumeAt].Cursor, batchSize: 2);

            Assert.That(resumed.SelectMany(batch => batch.Rows).Select(row => row.SourceId), Is.EqualTo(batches.Skip(resumeAt + 1).SelectMany(batch => batch.Rows).Select(row => row.SourceId)), $"resuming after batch {resumeAt}");
        }
    }

    [Test]
    public async Task A_second_source_reads_the_same_rows_because_the_anchor_comes_from_the_source([Values] bool throughIndex)
    {
        await UseReadPath(throughIndex);
        await Seed(FailedMessageStatus.Archived);
        await Seed(FailedMessageStatus.Unresolved);
        await Seed(FailedMessageStatus.Resolved);
        await Seed(FailedMessageStatus.Unresolved);
        await CompleteDatabaseOperation();

        var first = (await ReadAll()).Select(row => row.SourceId).ToArray();
        var restarted = (await ReadAll()).Select(row => row.SourceId).ToArray();

        Assert.That(first, Has.Length.EqualTo(4));
        Assert.That(restarted, Is.EqualTo(first), "a restart or a retry derives the same cutoff, with nothing persisted");
    }

    async Task UseReadPath(bool throughIndex)
    {
        if (!throughIndex)
        {
            await DocumentStore.Maintenance.SendAsync(new DeleteIndexOperation(new FailedMessageViewIndex().IndexName), TestContext.CurrentContext.CancellationToken);
        }
    }

    async Task<FailedMessage> Seed(FailedMessageStatus status) => await SeedFailedMessage(new IngestedFailure().ToFailedMessage(status));

    async Task<FailedMessage> Seed(FailedMessageStatus status, DateTime lastModified) => await SeedFailedMessage(new IngestedFailure().ToFailedMessage(status), lastModified);

    static DateTime TwoHoursAgo => DateTime.UtcNow.AddHours(-2);

    async Task<DateTime> LastModified(FailedMessage message)
    {
        using var session = DocumentStore.OpenAsyncSession();
        var loaded = await session.LoadAsync<FailedMessage>(message.Id);

        return DateTime.SpecifyKind(session.Advanced.GetLastModifiedFor(loaded).Value, DateTimeKind.Utc);
    }

    async Task SeedClaim(FailedMessage message)
    {
        using var session = DocumentStore.OpenAsyncSession();
        await session.StoreAsync(new FailedMessageRetry { FailedMessageId = message.Id, RetryBatchId = "RetryBatches/forwarded" }, RetryDocumentDataStore.MakeFailedMessageRetriesDocumentId(message.UniqueMessageId));
        await session.SaveChangesAsync();
    }

    async Task<List<MigrationRow>> ReadAll()
    {
        await using var source = await OpenMigrationSource();

        return (await CollectBatches(source, Category)).SelectMany(batch => batch.Rows).ToList();
    }

    async Task<long> CountThrough()
    {
        await using var source = await OpenMigrationSource();

        return await source.Count(Category);
    }
}
