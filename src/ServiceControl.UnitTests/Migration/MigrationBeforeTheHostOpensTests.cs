namespace ServiceControl.UnitTests.Migration;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Migration;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.UnitTests.Migration.Fakes;

[TestFixture]
class MigrationBeforeTheHostOpensTests
{
    [Test]
    public async Task A_persister_without_checkpoints_starts_without_a_migration_source()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        await new MigrationBeforeTheHostOpens(services).StartingAsync();
    }

    [Test]
    public async Task An_empty_checkpoint_table_starts_without_a_migration_source()
    {
        using var services = Services(new InMemoryMigrationCheckpointStore());
        await new MigrationBeforeTheHostOpens(services).StartingAsync();
    }

    [TestCase(MigrationCategoryState.NotStarted)]
    [TestCase(MigrationCategoryState.InProgress)]
    [TestCase(MigrationCategoryState.Blocked)]
    [TestCase(MigrationCategoryState.Halted)]
    [TestCase(MigrationCategoryState.CompleteWithErrors)]
    public async Task An_unfinished_required_copy_refuses_startup_without_resuming_it(MigrationCategoryState state)
    {
        var store = new InMemoryMigrationCheckpointStore();
        var before = await store.Upsert(Checkpoint(MigrationCategoryIds.KnownEndpoints, state));
        using var services = Services(store);

        var exception = Assert.ThrowsAsync<Exception>(() => new MigrationBeforeTheHostOpens(services).StartingAsync());
        Assert.That(exception.Message, Does.Contain("ServiceControl will not start"));
        Assert.That(await store.Read(before.CategoryId), Is.EqualTo(before), "startup must never run the offline copy");
    }

    [Test]
    public async Task An_unrecognized_unfinished_category_keeps_the_host_closed()
    {
        var store = new InMemoryMigrationCheckpointStore();
        await store.Upsert(Checkpoint("NewRequiredCategory", MigrationCategoryState.NotStarted));
        using var services = Services(store);
        var exception = Assert.ThrowsAsync<Exception>(() => new MigrationBeforeTheHostOpens(services).StartingAsync());
        Assert.That(exception.Message, Does.Contain("--migrate"));
    }

    [TestCase(MigrationCategoryState.NotStarted)]
    [TestCase(MigrationCategoryState.InProgress)]
    [TestCase(MigrationCategoryState.Halted)]
    [TestCase(MigrationCategoryState.CompleteWithErrors)]
    public async Task Optional_work_does_not_hold_the_host_closed(MigrationCategoryState state)
    {
        var store = new InMemoryMigrationCheckpointStore();
        await store.Upsert(Checkpoint(MigrationCategoryIds.KnownEndpoints, MigrationCategoryState.Complete));
        await store.Upsert(Checkpoint(MigrationCategoryIds.EventLog, state));
        using var services = Services(store);
        await new MigrationBeforeTheHostOpens(services).StartingAsync();
    }

    static ServiceProvider Services(IMigrationCheckpointStore store) =>
        new ServiceCollection().AddSingleton(store).BuildServiceProvider();

    static MigrationCheckpoint Checkpoint(string id, MigrationCategoryState state) =>
        new(id, state, null, 0, 0, null, null, null, null, null, null);
}
