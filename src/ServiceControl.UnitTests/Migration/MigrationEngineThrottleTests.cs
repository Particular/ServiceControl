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
class MigrationEngineThrottleTests
{
    static MigrationRow Row(string id) => new(id, new object(), new Dictionary<string, object?>());

    [Test]
    public async Task Optional_categories_pause_between_batches_for_the_configured_duration()
    {
        var category = MigrationCategoryRegistry.Find("EventLog")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"), Row("b"), Row("c"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 1 };
        var timeProvider = new FakeTimeProvider();
        var pause = TimeSpan.FromSeconds(1);
        var options = new MigrationEngineOptions(pause, 5, 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, timeProvider, options, NullLogger<MigrationEngine>.Instance);

        var runTask = engine.RunCategoryAsync(category);
        await Task.Delay(50);
        Assert.That(runTask.IsCompleted, Is.False, "the first inter-batch pause should still be pending");

        timeProvider.Advance(pause);
        await Task.Delay(50);
        Assert.That(runTask.IsCompleted, Is.False, "the second inter-batch pause should still be pending");

        timeProvider.Advance(pause);
        var checkpoint = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(checkpoint.CopiedCount, Is.EqualTo(3));
    }

    [Test]
    public async Task Required_categories_never_pause()
    {
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"), Row("b"), Row("c"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 1 };
        // A FakeTimeProvider that is never advanced: if the engine tried to pause, this would hang
        // until the test runner's own timeout, which WaitAsync turns into a clear failure instead.
        var options = new MigrationEngineOptions(TimeSpan.FromSeconds(1), 5, 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(checkpoint.CopiedCount, Is.EqualTo(3));
    }
}
