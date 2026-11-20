namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.ExternalIntegrations;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore;
using ServiceControl.Persistence.MessageRedirects;
using ServiceControl.Recoverability;
using ServiceControl.Recoverability.ExternalIntegration;

[TestFixture]
[NonParallelizable]
class RetryResetMigrationTests : MigrationAcceptanceTest
{
    [Test]
    public async Task A_retry_of_retry_operations_leaves_both_tables_as_one_copy_left_them()
    {
        await SeedSourceRetryHistory(TwoHistoricAndTwoPending());
        await CopyCategory(MigrationCategoryIds.RetryOperations);
        var copied = await Target.Count(RetryOperations);

        var saved = await Target.ResetForRetry(ResetForRetry(await ReadCheckpoint(MigrationCategoryIds.RetryOperations)));
        var stored = await ReadCheckpoint(MigrationCategoryIds.RetryOperations);
        var second = await CopyCategory(MigrationCategoryIds.RetryOperations);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved.Version, Is.EqualTo(stored.Version), "the returned checkpoint carries the version the reset saved");
            Assert.That(saved.State, Is.EqualTo(MigrationCategoryState.NotStarted));
            Assert.That(copied, Is.EqualTo(4), "the first copy fills both tables");
            Assert.That(await Target.Count(RetryOperations), Is.EqualTo(copied), "a historic row gets a new key on every insert, so a retry that kept them would double the history");
            Assert.That(second.AlreadyPresent, Is.Zero, "the pending table was emptied too, so the re-read finds none of its rows already present");
        }
    }

    [Test]
    public async Task A_retry_of_a_category_whose_keys_the_copy_supplies_deletes_nothing()
    {
        await SeedSourceMessageRedirects(
            new MessageRedirect { FromPhysicalAddress = "old1", ToPhysicalAddress = "new1", LastModified = ChangedAt },
            new MessageRedirect { FromPhysicalAddress = "old2", ToPhysicalAddress = "new2", LastModified = ChangedAt });
        await SeedSourceRetryHistory(TwoHistoricAndTwoPending());
        await CopyCategory(MigrationCategoryIds.MessageRedirects);
        await CopyCategory(MigrationCategoryIds.RetryOperations);

        await Target.ResetForRetry(ResetForRetry(await ReadCheckpoint(MigrationCategoryIds.MessageRedirects)));
        var second = await CopyCategory(MigrationCategoryIds.MessageRedirects);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second.AlreadyPresent, Is.EqualTo(2), "rows already in SQL are left alone, so the re-read finds both");
            Assert.That(await Target.Count(RetryOperations), Is.EqualTo(4), "the reset deletes only for the category it names, so another category's copied rows stay");
        }
    }

    [Test]
    public async Task A_stale_reset_deletes_nothing()
    {
        await SeedSourceRetryHistory(TwoHistoricAndTwoPending());
        await CopyCategory(MigrationCategoryIds.RetryOperations);
        var stale = await ReadCheckpoint(MigrationCategoryIds.RetryOperations);
        await QueryTarget(dbContext => dbContext.UpsertCheckpoint(stale));

        Assert.ThrowsAsync<MigrationCheckpointConflictException>(() => Target.ResetForRetry(ResetForRetry(stale)));
        Assert.That(await Target.Count(RetryOperations), Is.EqualTo(4), "the delete shares the reset's transaction, so a refused reset leaves every copied row");
    }

    [Test]
    public async Task A_retry_of_pending_integration_events_dispatches_each_event_once()
    {
        await SeedSource(
            ($"ExternalIntegrationDispatchRequests/{Guid.NewGuid()}", new ExternalIntegrationDispatchRequest { DispatchContext = new FailedMessageArchivedPublisher.DispatchContext { FailedMessageId = "FailedMessages/1" } }),
            ($"ExternalIntegrationDispatchRequests/{Guid.NewGuid()}", new ExternalIntegrationDispatchRequest { DispatchContext = new FailedMessageArchivedPublisher.DispatchContext { FailedMessageId = "FailedMessages/2" } }));
        await CopyCategory(MigrationCategoryIds.PendingIntegrationEvents);
        var copied = await Target.Count(PendingIntegrationEvents);

        await Target.ResetForRetry(ResetForRetry(await ReadCheckpoint(MigrationCategoryIds.PendingIntegrationEvents)));
        await CopyCategory(MigrationCategoryIds.PendingIntegrationEvents);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(copied, Is.EqualTo(2), "the first copy writes both requests");
            Assert.That(await Target.Count(PendingIntegrationEvents), Is.EqualTo(copied), "each request gets a new key on every insert, so a retry that kept them would dispatch every event twice");
        }
    }

    // The same reset --migration-retry makes. It keeps StartedAt and leaves SourceTotal alone, because the engine does not write it.
    internal static MigrationCheckpoint ResetForRetry(MigrationCheckpoint checkpoint) => checkpoint with
    {
        State = MigrationCategoryState.NotStarted,
        Cursor = null,
        CopiedCount = 0,
        SkippedCount = 0,
        AlreadyPresentCount = 0,
        SkipReasons = null,
        SettledAt = null,
        LastError = null
    };

    static RetryHistory TwoHistoricAndTwoPending()
    {
        var history = new RetryHistory();
        history.HistoricOperations.Add(RetryOperationsMigrationTests.HistoricOperation("group-1", ChangedAt));
        history.HistoricOperations.Add(RetryOperationsMigrationTests.HistoricOperation("group-2", ChangedAt.AddMinutes(-10)));
        history.UnacknowledgedOperations.Add(RetryOperationsMigrationTests.PendingOperation("group-3", ChangedAt));
        history.UnacknowledgedOperations.Add(RetryOperationsMigrationTests.PendingOperation("group-4", ChangedAt.AddMinutes(-10)));
        return history;
    }

    static MigrationCategory PendingIntegrationEvents => MigrationCategoryRegistry.Find(MigrationCategoryIds.PendingIntegrationEvents)!;

    static MigrationCategory RetryOperations => MigrationCategoryRegistry.Find(MigrationCategoryIds.RetryOperations)!;

    static readonly DateTime ChangedAt = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
}
