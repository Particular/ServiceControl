#nullable enable
namespace ServiceControl.UnitTests.Migration;

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Migration;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.UnitTests.Migration.Fakes;

[TestFixture]
class RecordSourceOutageTests
{
    static readonly MigrationCategory EventLog = MigrationCategoryRegistry.Find(MigrationCategoryIds.EventLog)!;
    static readonly MigrationCategory ArchivedAndResolvedFailedMessages = MigrationCategoryRegistry.Find(MigrationCategoryIds.ArchivedAndResolvedFailedMessages)!;

    readonly InMemoryMigrationCheckpointStore checkpointStore = new();

    [Test]
    public async Task A_category_still_copying_keeps_its_state_and_gains_the_error()
    {
        await checkpointStore.Upsert(new MigrationCheckpoint(MigrationCategoryIds.EventLog, MigrationCategoryState.InProgress, "EventLogItem/40", 40, 0, null, null, null, null, null, null));

        await RecordOutage();

        var row = await checkpointStore.Read(MigrationCategoryIds.EventLog);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row!.State, Is.EqualTo(MigrationCategoryState.InProgress));
            Assert.That(row.Cursor, Is.EqualTo("EventLogItem/40"));
            Assert.That(row.CopiedCount, Is.EqualTo(40));
            Assert.That(row.SkippedCount, Is.Zero);
            Assert.That(row.LastError, Does.Contain("RavenDB is unreachable").And.Contain("next start"));
        }
    }

    [Test]
    public async Task A_category_with_no_row_gets_a_not_started_row_carrying_the_error()
    {
        await RecordOutage();

        var row = await checkpointStore.Read(MigrationCategoryIds.ArchivedAndResolvedFailedMessages);

        Assert.That(row, Is.Not.Null, "status and verify read only the rows that exist, so a category with no row would show no error");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row!.State, Is.EqualTo(MigrationCategoryState.NotStarted));
            Assert.That(row.CopiedCount, Is.Zero);
            Assert.That(row.SkippedCount, Is.Zero);
            Assert.That(row.AlreadyPresentCount, Is.Zero);
            Assert.That(row.StartedAt, Is.Null, "a category that never started must not look as if it had");
            Assert.That(row.LastError, Does.Contain("RavenDB is unreachable").And.Contain("next start"));
        }
    }

    [TestCase(MigrationCategoryState.Complete)]
    [TestCase(MigrationCategoryState.Halted)]
    [TestCase(MigrationCategoryState.CompleteWithErrors)]
    [TestCase(MigrationCategoryState.Abandoned)]
    public async Task A_settled_category_is_left_alone(MigrationCategoryState state)
    {
        var before = await checkpointStore.Upsert(new MigrationCheckpoint(MigrationCategoryIds.EventLog, state, null, 0, 0, null, null, null, null, null, "earlier"));

        await RecordOutage();

        var after = await checkpointStore.Read(MigrationCategoryIds.EventLog);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(after!.Version, Is.EqualTo(before.Version), "a settled row is never saved again");
            Assert.That(after.State, Is.EqualTo(state));
            Assert.That(after.LastError, Is.EqualTo("earlier"));
        }
    }

    Task RecordOutage() =>
        MigrationStartup.RecordSourceOutage(checkpointStore, [EventLog, ArchivedAndResolvedFailedMessages], new InvalidOperationException("RavenDB is unreachable"));
}
