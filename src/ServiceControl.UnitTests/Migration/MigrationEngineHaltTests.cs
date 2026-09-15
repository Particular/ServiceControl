#nullable enable
namespace ServiceControl.UnitTests.Migration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.UnitTests.Migration.Fakes;

[TestFixture]
class MigrationEngineHaltTests
{
    static MigrationRow Row(string id) => new(id, new object(), new Dictionary<string, object?>());

    [Test]
    public async Task A_systemic_failure_halts_the_category_partway_through()
    {
        var category = MigrationCategoryRegistry.Find("ArchivedAndResolvedFailedMessages")!;
        var source = new InMemoryMigrationSource();
        // Every 5th of 1,000 rows is rejected: a steady 20% spread evenly rather than clustered at the
        // start, past both the 5% proportion and the 100-row floor.
        var rows = Enumerable.Range(1, 1_000).Select(i => Row($"row-{i}")).ToArray();
        source.Seed(category.Id, rows);
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 100 };
        foreach (var i in Enumerable.Range(1, 1_000).Where(i => i % 5 == 0))
        {
            target.RejectKey($"row-{i}", "Rejected");
        }
        var options = new MigrationEngineOptions(TimeSpan.Zero, HaltThresholdPercent: 5, HaltThresholdMinimum: 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Halted));
            Assert.That(checkpoint.LastError, Does.Contain("Halted"));
            Assert.That(checkpoint.CompletedAt, Is.Not.Null);
            // Stopped partway: the 1,000th row was never reached.
            Assert.That(target.WrittenRows(category.Id).Count, Is.LessThan(800));
        }
    }

    [Test]
    public async Task Rows_already_present_in_the_target_never_count_toward_the_halt_threshold()
    {
        var category = MigrationCategoryRegistry.Find("ArchivedAndResolvedFailedMessages")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, [.. Enumerable.Range(1, 1_000).Select(i => Row($"row-{i}"))]);
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 100 };
        foreach (var i in Enumerable.Range(1, 1_000).Where(i => i % 5 == 0))
        {
            target.SeedExistingKey($"row-{i}");
        }
        var options = new MigrationEngineOptions(TimeSpan.Zero, HaltThresholdPercent: 5, HaltThresholdMinimum: 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Complete));
            Assert.That(checkpoint.CopiedCount, Is.EqualTo(800));
            Assert.That(checkpoint.AlreadyPresentCount, Is.EqualTo(200));
        }
    }

    [Test]
    public void Halted_is_distinct_from_Complete_and_CompleteWithErrors()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MigrationCategoryState.Halted, Is.Not.EqualTo(MigrationCategoryState.Complete));
            Assert.That(MigrationCategoryState.Halted, Is.Not.EqualTo(MigrationCategoryState.CompleteWithErrors));
            Assert.That(MigrationCategoryState.CompleteWithErrors, Is.Not.EqualTo(MigrationCategoryState.Complete));
        }
    }
}
