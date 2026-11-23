#nullable enable
namespace ServiceControl.UnitTests.Migration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using ServiceControl.Migration;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.UnitTests.Migration.Fakes;

[TestFixture]
class MigrationOperatorCommandsTests
{
    static readonly DateTime StartedTwoDaysAgo = new(2026, 9, 9, 10, 0, 0, DateTimeKind.Utc);
    static readonly DateTime AbandonedAt = new(2026, 9, 11, 9, 30, 0, DateTimeKind.Utc);
    static readonly DateTime AbandonedLater = new(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);

    [TestCase(MigrationCategoryState.CompleteWithErrors, 3L, MigrationSkipReason.BodyUnreadable)]
    public void A_failed_category_is_read_again_from_the_start(MigrationCategoryState state, long skipped, MigrationSkipReason? reason)
    {
        var failed = Checkpoint("UnresolvedAndRetryIssuedFailedMessages", state, cursor: "cursor-9", copied: 997, skipped: skipped, lastError: "TimeoutException at cursor cursor-9: timed out",
            skipReasons: reason is null ? null : new Dictionary<MigrationSkipReason, long> { [reason.Value] = skipped },
            startedAt: StartedTwoDaysAgo, version: 7);

        var retried = MigrationOperatorCommands.Retry(failed);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(retried.State, Is.EqualTo(MigrationCategoryState.NotStarted));
            Assert.That(retried.Cursor, Is.Null, "a retry never reuses the cursor, so an expired or unreadable one cannot fail the category again");
            Assert.That((retried.CopiedCount, retried.SkippedCount, retried.AlreadyPresentCount), Is.EqualTo((0L, 0L, 0L)), "a re-read counts again from nothing, or the fault skips it already fixed would keep the category Failed");
            Assert.That(retried.SkipReasons, Is.Null);
            Assert.That(retried.StartedAt, Is.EqualTo(StartedTwoDaysAgo), "--migration-status still shows when the category first started");
            Assert.That((retried.LastError, retried.SettledAt), Is.EqualTo(((string?)null, (DateTime?)null)));
            Assert.That(retried.Version, Is.EqualTo(failed.Version), "the reset is saved against the version that was read, so a race fails loudly");
        }
    }

    [TestCase("KnownEndpoints", MigrationCategoryState.NotStarted, null, 0L, "TimeoutException at cursor FailedMessages/40: RavenDB stopped answering")]
    [TestCase("KnownEndpoints", MigrationCategoryState.InProgress, "cursor-9", 40L, "TimeoutException at cursor cursor-9: timed out")]
    [TestCase("KnownEndpoints", MigrationCategoryState.Blocked, null, 0L, null)]
    [TestCase("KnownEndpoints", MigrationCategoryState.Complete, null, 0L, null)]
    [TestCase("KnownEndpoints", MigrationCategoryState.Abandoned, null, 0L, null)]
    public void Only_a_failed_category_can_be_retried(string categoryId, MigrationCategoryState state, string? cursor, long copied, string? lastError) =>
        Assert.That(() => MigrationOperatorCommands.Retry(Checkpoint(categoryId, state, cursor: cursor, copied: copied, lastError: lastError)),
            Throws.InvalidOperationException.With.Message.Contain("only a Failed category can be retried"),
            "an error alone does not make a category Failed, and the next run resumes any category that is not Failed");

    [Test]
    public async Task A_retry_racing_a_running_copy_fails_and_changes_nothing()
    {
        var store = new InMemoryMigrationCheckpointStore();
        await store.Upsert(Checkpoint("KnownEndpoints", MigrationCategoryState.Halted, cursor: "cursor-9", copied: 40, lastError: "timed out"));
        var racing = new RacingCheckpointStore(store);

        Assert.ThrowsAsync<MigrationCheckpointConflictException>(() => MigrationOperatorCommands.ApplyRetry(racing, new InMemoryMigrationTarget(racing), "KnownEndpoints"));

        Assert.That((await store.Read("KnownEndpoints"))!.State, Is.EqualTo(MigrationCategoryState.Halted), "the copy's own write stands");
    }

    [Test]
    public void Abandoning_keeps_the_counts_and_the_reason()
    {
        var failed = Checkpoint("UnresolvedAndRetryIssuedFailedMessages", MigrationCategoryState.CompleteWithErrors, cursor: "cursor-9", copied: 997, skipped: 3, lastError: "3 rows were skipped as faults",
            skipReasons: new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.BodyUnreadable] = 3 },
            startedAt: StartedTwoDaysAgo, version: 7);

        var abandoned = MigrationOperatorCommands.Abandon(failed, AbandonedAt);

        Assert.That(abandoned, Is.EqualTo(failed with { State = MigrationCategoryState.Abandoned, SettledAt = AbandonedAt }),
            "the cursor, counts, reasons and error are the size of the loss and why it happened, so an abandon keeps them");
    }

    [TestCase("KnownEndpoints", MigrationCategoryState.NotStarted, true)]
    public void An_unfinished_category_can_be_abandoned(string categoryId, MigrationCategoryState state, bool started)
    {
        var startedAt = started ? StartedTwoDaysAgo : (DateTime?)null;

        var abandoned = MigrationOperatorCommands.Abandon(Checkpoint(categoryId, state, startedAt: startedAt), AbandonedAt);

        Assert.That((abandoned.State, abandoned.StartedAt), Is.EqualTo((MigrationCategoryState.Abandoned, startedAt)),
            "a reset row is unfinished, so it can be abandoned, and abandoning keeps its StartedAt");
    }

    [TestCase(MigrationCategoryState.Complete, "Done")]
    [TestCase(MigrationCategoryState.Abandoned, "Abandoned")]
    public async Task A_finished_category_cannot_be_abandoned_and_abandons_no_dependant(MigrationCategoryState state, string word)
    {
        var store = new InMemoryMigrationCheckpointStore();
        var finished = await store.Upsert(Checkpoint("KnownEndpoints", state, copied: 40, startedAt: StartedTwoDaysAgo));
        var settings = await store.Upsert(Checkpoint("EndpointSettings", MigrationCategoryState.Halted, cursor: "cursor-9", copied: 40, lastError: "timed out"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => MigrationOperatorCommands.Abandon(finished, AbandonedAt), Throws.InvalidOperationException.With.Message.Contain($"KnownEndpoints is {word}"),
                "a finished category has nothing left to give up");
            Assert.That(() => MigrationOperatorCommands.ApplyAbandon(store, "KnownEndpoints", AbandonedAt, NullLogger.Instance),
                Throws.InvalidOperationException.With.Message.Contain($"KnownEndpoints is {word} and cannot be abandoned."));
            Assert.That(await store.Read("EndpointSettings"), Is.EqualTo(settings), "a refused abandon gives up nothing, so endpoint settings can still be retried");
        }
    }

    [Test]
    public void Dependants_are_found_through_every_data_link_deepest_first()
    {
        var a = new MigrationCategory("A", MigrationCategoryKind.Required, CarriesBodies: false, Order: 1);
        var b = new MigrationCategory("B", MigrationCategoryKind.Required, CarriesBodies: false, Order: 2, MustFollow: "A", MustFollowIsDataLink: true);
        var c = new MigrationCategory("C", MigrationCategoryKind.Required, CarriesBodies: false, Order: 3, MustFollow: "B", MustFollowIsDataLink: true);
        var d = new MigrationCategory("D", MigrationCategoryKind.Required, CarriesBodies: false, Order: 4);

        Assert.That(MigrationOperatorCommands.CategoriesThatNeedItsData("A", [a, b, c, d]), Is.EqualTo(new[] { "C", "B" }));
    }

    [Test]
    public async Task An_ordering_only_follower_is_not_abandoned()
    {
        var store = new InMemoryMigrationCheckpointStore();
        await store.Upsert(Checkpoint("UnresolvedAndRetryIssuedFailedMessages", MigrationCategoryState.Halted, cursor: "cursor-9", copied: 40, lastError: "timed out"));
        var comments = await store.Upsert(Checkpoint("GroupComments", MigrationCategoryState.Blocked));

        var abandoned = await MigrationOperatorCommands.ApplyAbandon(store, "UnresolvedAndRetryIssuedFailedMessages", AbandonedAt, NullLogger.Instance);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(abandoned.Select(row => row.CategoryId), Is.EqualTo(new[] { "UnresolvedAndRetryIssuedFailedMessages" }));
            Assert.That(await store.Read("GroupComments"), Is.EqualTo(comments), "group comments follow failed messages only for ordering, so they still copy once the predecessor is finished");
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Abandoning_a_category_abandons_every_category_that_needs_its_data(bool dependantHasRow)
    {
        var store = new InMemoryMigrationCheckpointStore();
        await store.Upsert(Checkpoint("KnownEndpoints", MigrationCategoryState.Halted, cursor: "cursor-9", copied: 40, lastError: "timed out"));
        if (dependantHasRow)
        {
            await store.Upsert(Checkpoint("EndpointSettings", MigrationCategoryState.Blocked));
        }

        var abandoned = await MigrationOperatorCommands.ApplyAbandon(store, "KnownEndpoints", AbandonedAt, NullLogger.Instance);

        var settings = (await store.Read("EndpointSettings"))!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(abandoned.Select(row => row.CategoryId), Is.EqualTo(new[] { "EndpointSettings", "KnownEndpoints" }),
                "without a row, the next copy would seed endpoint settings and copy them against known endpoints that were never copied");
            Assert.That((settings.State, settings.SettledAt, settings.Cursor), Is.EqualTo((MigrationCategoryState.Abandoned, (DateTime?)AbandonedAt, (string?)null)),
                "settings copied for endpoints that were never copied would be deleted by the heartbeat settings sync");
            Assert.That((settings.CopiedCount, settings.SkippedCount, settings.AlreadyPresentCount), Is.EqualTo((0L, 0L, 0L)));
            Assert.That(await StateAndSettledAt(store, "KnownEndpoints"), Is.EqualTo((MigrationCategoryState.Abandoned, (DateTime?)AbandonedAt)));
        }
    }

    [Test]
    public async Task A_cascade_stopped_part_way_is_finished_by_running_the_command_again()
    {
        var store = new InMemoryMigrationCheckpointStore();
        await store.Upsert(Checkpoint("KnownEndpoints", MigrationCategoryState.Halted, cursor: "cursor-9", copied: 40, lastError: "timed out"));
        await store.Upsert(Checkpoint("EndpointSettings", MigrationCategoryState.Blocked));

        Assert.ThrowsAsync<TimeoutException>(() => MigrationOperatorCommands.ApplyAbandon(new FailingOnThirdUpsertCheckpointStore(store), "KnownEndpoints", AbandonedAt, NullLogger.Instance));

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await store.Read("EndpointSettings"))!.State, Is.EqualTo(MigrationCategoryState.Abandoned));
            Assert.That((await store.Read("KnownEndpoints"))!.State, Is.EqualTo(MigrationCategoryState.Halted), "the named category is saved last, so a stopped command can be run again");
        }

        var abandoned = await MigrationOperatorCommands.ApplyAbandon(store, "KnownEndpoints", AbandonedLater, NullLogger.Instance);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(abandoned.Select(row => row.CategoryId), Is.EqualTo(new[] { "KnownEndpoints" }));
            Assert.That(await StateAndSettledAt(store, "KnownEndpoints"), Is.EqualTo((MigrationCategoryState.Abandoned, (DateTime?)AbandonedLater)));
            Assert.That((await store.Read("EndpointSettings"))!.SettledAt, Is.EqualTo(AbandonedAt), "an Abandoned dependant is final, so the second run leaves it alone");
        }
    }

    [Test]
    public async Task An_abandon_racing_a_copy_of_the_named_category_abandons_no_dependant()
    {
        var store = new InMemoryMigrationCheckpointStore();
        await store.Upsert(Checkpoint("KnownEndpoints", MigrationCategoryState.InProgress, cursor: "cursor-9", copied: 40));
        var settings = await store.Upsert(Checkpoint("EndpointSettings", MigrationCategoryState.Blocked));

        Assert.ThrowsAsync<MigrationCheckpointConflictException>(() => MigrationOperatorCommands.ApplyAbandon(new RacingCheckpointStore(store), "KnownEndpoints", AbandonedAt, NullLogger.Instance));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await store.Read("EndpointSettings"), Is.EqualTo(settings), "a copy still running on known endpoints would finish them under abandoned settings");
            Assert.That((await store.Read("KnownEndpoints"))!.State, Is.EqualTo(MigrationCategoryState.InProgress));
        }
    }

    static async Task<(MigrationCategoryState, DateTime?)> StateAndSettledAt(IMigrationCheckpointStore store, string categoryId)
    {
        var checkpoint = (await store.Read(categoryId))!;
        return (checkpoint.State, checkpoint.SettledAt);
    }

    static MigrationCheckpoint Checkpoint(
        string categoryId,
        MigrationCategoryState state,
        string? cursor = null,
        long copied = 0,
        long skipped = 0,
        string? lastError = null,
        IReadOnlyDictionary<MigrationSkipReason, long>? skipReasons = null,
        DateTime? startedAt = null,
        long version = 0,
        long alreadyPresent = 0) =>
        new(
            CategoryId: categoryId,
            State: state,
            Cursor: cursor,
            CopiedCount: copied,
            SkippedCount: skipped,
            SourceTotal: null,
            SkipReasons: skipReasons,
            StartedAt: startedAt,
            LastProgressAt: null,
            SettledAt: null,
            LastError: lastError,
            AlreadyPresentCount: alreadyPresent,
            Version: version);

    // Its first Read saves the row once more before handing it back, which is what a copy committing a batch between the command's read and its write looks like.
    sealed class RacingCheckpointStore(IMigrationCheckpointStore inner) : IMigrationCheckpointStore
    {
        bool raced;

        public Task<IReadOnlyList<MigrationCheckpoint>> ReadAll(CancellationToken cancellationToken = default) => inner.ReadAll(cancellationToken);

        public async Task<MigrationCheckpoint?> Read(string categoryId, CancellationToken cancellationToken = default)
        {
            var stored = await inner.Read(categoryId, cancellationToken);

            if (!raced && stored is not null)
            {
                raced = true;
                await inner.Upsert(stored, cancellationToken);
            }

            return stored;
        }

        public Task<MigrationCheckpoint> Upsert(MigrationCheckpoint checkpoint, CancellationToken cancellationToken = default) => inner.Upsert(checkpoint, cancellationToken);
    }

    sealed class FailingOnThirdUpsertCheckpointStore(IMigrationCheckpointStore inner) : IMigrationCheckpointStore
    {
        int upserts;

        public Task<IReadOnlyList<MigrationCheckpoint>> ReadAll(CancellationToken cancellationToken = default) => inner.ReadAll(cancellationToken);

        public Task<MigrationCheckpoint?> Read(string categoryId, CancellationToken cancellationToken = default) => inner.Read(categoryId, cancellationToken);

        public Task<MigrationCheckpoint> Upsert(MigrationCheckpoint checkpoint, CancellationToken cancellationToken = default) =>
            ++upserts == 3 ? throw new TimeoutException("The database did not answer.") : inner.Upsert(checkpoint, cancellationToken);
    }
}
