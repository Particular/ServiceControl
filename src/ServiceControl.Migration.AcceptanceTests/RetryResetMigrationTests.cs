namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.ExternalIntegrations;
using ServiceControl.Migration;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.MessageRedirects;
using ServiceControl.Recoverability;
using ServiceControl.Recoverability.ExternalIntegration;

[TestFixture]
[NonParallelizable]
class RetryResetMigrationTests : MigrationAcceptanceTest
{
    [Test]
    public async Task A_retry_of_a_category_whose_keys_the_copy_supplies_deletes_nothing()
    {
        await SeedSourceMessageRedirects(
            new MessageRedirect { FromPhysicalAddress = "old1", ToPhysicalAddress = "new1", LastModified = ChangedAt },
            new MessageRedirect { FromPhysicalAddress = "old2", ToPhysicalAddress = "new2", LastModified = ChangedAt });
        await SeedSourceRetryHistory(TwoHistoricAndTwoPending());
        await CopyCategory(MigrationCategoryIds.MessageRedirects);
        await CopyCategory(MigrationCategoryIds.RetryOperations);
        await SeedCheckpoint(await ReadCheckpoint(MigrationCategoryIds.MessageRedirects) with { State = MigrationCategoryState.Halted, LastError = "test" });
        await TargetReadiness.RecordHostOpened();

        var reset = await MigrationOperatorCommands.ApplyRetry(CheckpointStore, Target, MigrationCategoryIds.MessageRedirects);
        var stored = await ReadCheckpoint(MigrationCategoryIds.MessageRedirects);
        var second = await CopyCategory(MigrationCategoryIds.MessageRedirects);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reset.State, Is.EqualTo(MigrationCategoryState.NotStarted), "a retry that keeps every row loses nothing, so the marker must not stop it");
            Assert.That(stored.State, Is.EqualTo(MigrationCategoryState.NotStarted), "the reset is saved, not only returned");
            Assert.That(second.AlreadyPresent, Is.EqualTo(2), "rows already in SQL are left alone, so the re-read finds both");
            Assert.That(await Target.Count(RetryOperations), Is.EqualTo(4), "the reset deletes only for the category it names, so another category's copied rows stay");
        }
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

    // The reset a retry makes, from a row made Failed first so the product's own rule accepts it.
    internal static MigrationCheckpoint ResetForRetry(MigrationCheckpoint checkpoint) =>
        MigrationOperatorCommands.Retry(checkpoint with { State = MigrationCategoryState.Halted });

    internal static RetryHistory TwoHistoricAndTwoPending()
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
