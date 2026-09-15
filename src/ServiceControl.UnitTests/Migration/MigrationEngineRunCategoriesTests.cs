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
        var ordered = engine.SelectCategories(MigrationCategoryKind.Required).Take(2).ToArray();
        foreach (var category in ordered)
        {
            source.Seed(category.Id, Row($"{category.Id}-1"));
        }

        var results = await engine.RunCategories(ordered);

        using (Assert.EnterMultipleScope())
        {
            // KnownEndpoints then EndpointSettings, the order the registry declares, whichever order
            // the caller passed them in.
            Assert.That(results.Select(c => c.CategoryId), Is.EqualTo(new[] { "KnownEndpoints", "EndpointSettings" }));
            Assert.That(results.Select(c => c.State), Is.All.EqualTo(MigrationCategoryState.Complete));
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
