#nullable enable
namespace ServiceControl.UnitTests.Migration;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.UnitTests.Migration.Fakes;

[TestFixture]
class MigrationEngineOrderingTests
{
    static MigrationRow Row(string id) => new(id, new object(), new Dictionary<string, object?>());

    static MigrationEngine BuildEngine(InMemoryMigrationSource source, InMemoryMigrationCheckpointStore checkpointStore, InMemoryMigrationTarget target) =>
        new(source, target, checkpointStore, new FakeTimeProvider(), new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []), NullLogger<MigrationEngine>.Instance);

    [Test]
    public async Task LicensingThroughput_does_not_start_before_LicensingEndpoints_completes_and_says_so_on_its_checkpoint_row()
    {
        var throughputCategory = MigrationCategoryRegistry.Find("LicensingThroughput")!;
        var source = new InMemoryMigrationSource();
        source.Seed(throughputCategory.Id, Row("t-1"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore);
        var engine = BuildEngine(source, checkpointStore, target);

        // LicensingEndpoints has never run: no checkpoint row for it at all.
        var checkpoint = await engine.RunCategoryAsync(throughputCategory);

        var persisted = await checkpointStore.Read(throughputCategory.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Blocked));
            Assert.That(target.WrittenRows(throughputCategory.Id), Is.Empty);
            // A row exists, so status can print it. Without one, an operator cannot tell a category
            // waiting on another from a category nobody asked for.
            Assert.That(persisted, Is.Not.Null);
            Assert.That(persisted!.State, Is.EqualTo(MigrationCategoryState.Blocked));
            Assert.That(persisted.LastError, Does.Contain("LicensingEndpoints"));
        }
    }

    [Test]
    public async Task EndpointSettings_does_not_start_before_KnownEndpoints_completes()
    {
        // Not a foreign key: settings wait for known endpoints, and a halted required category keeps
        // the host, and with it the heartbeat sync, closed.
        var settingsCategory = MigrationCategoryRegistry.Find("EndpointSettings")!;
        var source = new InMemoryMigrationSource();
        source.Seed(settingsCategory.Id, Row("EndpointSettings/1"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore);
        var engine = BuildEngine(source, checkpointStore, target);

        var checkpoint = await engine.RunCategoryAsync(settingsCategory);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Blocked));
            Assert.That(target.WrittenRows(settingsCategory.Id), Is.Empty);
        }
    }

    [TestCase(MigrationCategoryState.InProgress)]
    [TestCase(MigrationCategoryState.Halted)]
    public async Task GroupComments_does_not_start_before_the_archive_completes(MigrationCategoryState archiveState)
    {
        var comments = MigrationCategoryRegistry.Find("GroupComments")!;
        var source = new InMemoryMigrationSource();
        source.Seed(comments.Id, Row("GroupComment/g-1"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        await checkpointStore.Upsert(new MigrationCheckpoint("ArchivedAndResolvedFailedMessages", archiveState, "m-500", 500, 0, null, null, DateTime.UtcNow, DateTime.UtcNow, null, null));
        var target = new InMemoryMigrationTarget(checkpointStore);
        var engine = BuildEngine(source, checkpointStore, target);

        var checkpoint = await engine.RunCategoryAsync(comments);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Blocked));
            Assert.That(checkpoint.LastError, Is.EqualTo($"Blocked: GroupComments must follow ArchivedAndResolvedFailedMessages, which is {archiveState}"));
            Assert.That(target.WrittenRows(comments.Id), Is.Empty);
        }
    }

    [TestCase(MigrationCategoryState.Complete)]
    [TestCase(MigrationCategoryState.CompleteWithErrors)]
    [TestCase(MigrationCategoryState.Abandoned)]
    public async Task LicensingThroughput_proceeds_once_LicensingEndpoints_is_finished_or_abandoned(MigrationCategoryState endpointsState)
    {
        var throughputCategory = MigrationCategoryRegistry.Find("LicensingThroughput")!;
        var source = new InMemoryMigrationSource();
        source.Seed(throughputCategory.Id, Row("t-1"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        await checkpointStore.Upsert(new MigrationCheckpoint("LicensingEndpoints", endpointsState, "e-1", 1, 0, 1, null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, null));
        var target = new InMemoryMigrationTarget(checkpointStore);
        var engine = BuildEngine(source, checkpointStore, target);

        var checkpoint = await engine.RunCategoryAsync(throughputCategory);

        Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Complete));
    }
}
