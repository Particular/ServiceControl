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
class MigrationEngineRunCategoriesTests
{
    static MigrationRow Row(string id) => new(id, new object(), new Dictionary<string, object?>());

    [Test]
    public async Task Runs_every_category_it_is_given_in_the_order_it_is_given_them()
    {
        var source = new InMemoryMigrationSource();
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(),
            new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []), NullLogger<MigrationEngine>.Instance);
        // The reverse of registry order, so an engine that re-sorted by Order would run KnownEndpoints first.
        MigrationCategory[] given = [MigrationCategoryRegistry.Find("MessageRedirects")!, MigrationCategoryRegistry.Find("KnownEndpoints")!];
        foreach (var category in given)
        {
            source.Seed(category.Id, Row($"{category.Id}-1"));
        }

        var results = await engine.RunCategories(given);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results.Select(c => c.CategoryId), Is.EqualTo(new[] { "MessageRedirects", "KnownEndpoints" }));
            Assert.That(results.Select(c => c.State), Is.All.EqualTo(MigrationCategoryState.Complete));
        }
    }

    [Test]
    public async Task Every_category_it_is_given_has_a_row_before_the_first_one_copies_anything()
    {
        // A copy stopped between two categories must not leave a table that reads as finished, because the
        // gates that keep a host off an unfinished copy look only at the rows that exist.
        var source = new InMemoryMigrationSource();
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        IReadOnlyList<MigrationCheckpoint>? rowsAtFirstWrite = null;
        var target = new InMemoryMigrationTarget(checkpointStore) { BeforeWrite = _ => rowsAtFirstWrite ??= checkpointStore.ReadAll().GetAwaiter().GetResult() };
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(),
            new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []), NullLogger<MigrationEngine>.Instance);
        // Three, so recording only the next category would still leave the last one missing.
        MigrationCategory[] given = [MigrationCategoryRegistry.Find("KnownEndpoints")!, MigrationCategoryRegistry.Find("EndpointSettings")!, MigrationCategoryRegistry.Find("MessageRedirects")!];
        foreach (var category in given)
        {
            source.Seed(category.Id, Row($"{category.Id}-1"));
        }

        await engine.RunCategories(given);

        Assert.That(rowsAtFirstWrite, Is.Not.Null, "the copy never wrote a batch");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(rowsAtFirstWrite!.Select(c => c.CategoryId), Is.EquivalentTo(new[] { "KnownEndpoints", "EndpointSettings", "MessageRedirects" }));
            Assert.That(rowsAtFirstWrite!.Where(c => c.CategoryId != "KnownEndpoints").Select(c => c.State), Is.All.EqualTo(MigrationCategoryState.NotStarted));
        }
    }

    [Test]
    public async Task A_halted_category_is_left_untouched_and_the_next_category_still_runs()
    {
        var firstStarted = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var source = new InMemoryMigrationSource();
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var halted = await checkpointStore.Upsert(new MigrationCheckpoint("KnownEndpoints", MigrationCategoryState.Halted, "KnownEndpoints-1", 1, 0, null, null, firstStarted, firstStarted, firstStarted, "Halted: earlier run"));
        var target = new InMemoryMigrationTarget(checkpointStore);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(),
            new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []), NullLogger<MigrationEngine>.Instance);
        source.Seed("KnownEndpoints", Row("KnownEndpoints-1"), Row("KnownEndpoints-2"));
        // MessageRedirects does not follow KnownEndpoints, so a Failed KnownEndpoints cannot hold it back.
        source.Seed("MessageRedirects", Row("MessageRedirects-1"));

        var results = await engine.RunCategories([MigrationCategoryRegistry.Find("KnownEndpoints")!, MigrationCategoryRegistry.Find("MessageRedirects")!]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(target.RowsHandedToWrite("KnownEndpoints"), Is.Empty, "a Failed category waits for the operator");
            Assert.That(results[0], Is.EqualTo(halted), "the halted category's row was changed");
            Assert.That(await checkpointStore.Read("KnownEndpoints"), Is.EqualTo(halted));
            Assert.That(results[1].State, Is.EqualTo(MigrationCategoryState.Complete));
            Assert.That(target.WrittenRows("MessageRedirects").Select(row => row.SourceId), Is.EqualTo(new[] { "MessageRedirects-1" }));
        }
    }

    [Test]
    public async Task A_finished_category_is_not_copied_again()
    {
        var source = new InMemoryMigrationSource();
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        await checkpointStore.Upsert(new MigrationCheckpoint("KnownEndpoints", MigrationCategoryState.Complete, "KnownEndpoints-1", 1, 0, 1, null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, null));
        var target = new InMemoryMigrationTarget(checkpointStore);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(),
            new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []), NullLogger<MigrationEngine>.Instance);
        source.Seed("KnownEndpoints", Row("KnownEndpoints-1"));

        var results = await engine.RunCategories([MigrationCategoryRegistry.Find("KnownEndpoints")!, MigrationCategoryRegistry.Find("EndpointSettings")!]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(target.RowsHandedToWrite("KnownEndpoints"), Is.Empty);
            Assert.That(results[0].State, Is.EqualTo(MigrationCategoryState.Complete));
        }
    }

    [Test]
    public async Task Running_no_categories_copies_nothing_and_reports_nothing()
    {
        // What an instance that selected no optional categories asks for on every background pass.
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore);
        var engine = new MigrationEngine(new InMemoryMigrationSource(), target, checkpointStore, new FakeTimeProvider(),
            new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []), NullLogger<MigrationEngine>.Instance);

        var results = await engine.RunCategories([]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Is.Empty);
            Assert.That(await checkpointStore.ReadAll(), Is.Empty, "a category nobody ran gets no row");
        }
    }

    [Test]
    public async Task One_category_halting_does_not_stop_the_ones_after_it()
    {
        // Everything that can still be copied is copied, and the guard decides what an incomplete migration
        // may do. Stopping at the first halt would leave later categories untouched with no reason recorded.
        var source = new InMemoryMigrationSource();
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 1, FailOnCallNumber = 1 };
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(),
            new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []), NullLogger<MigrationEngine>.Instance);
        var first = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var second = MigrationCategoryRegistry.Find("MessageRedirects")!;
        source.Seed(first.Id, Row("k-1"));
        source.Seed(second.Id, Row("r-1"));

        var results = await engine.RunCategories([first, second]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results[0].State, Is.EqualTo(MigrationCategoryState.Halted));
            Assert.That(results[1].State, Is.EqualTo(MigrationCategoryState.Complete));
        }
    }
}
