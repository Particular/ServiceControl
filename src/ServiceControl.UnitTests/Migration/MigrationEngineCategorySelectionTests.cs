namespace ServiceControl.UnitTests.Migration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using Particular.Approvals;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.UnitTests.Migration.Fakes;

[TestFixture]
class MigrationEngineCategorySelectionTests
{
    static MigrationEngine BuildEngine(IReadOnlyCollection<string> selectedOptionalIds)
    {
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore);
        var options = new MigrationEngineOptions(TimeSpan.FromSeconds(1), 5, 100, selectedOptionalIds);
        return new MigrationEngine(new InMemoryMigrationSource(), target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);
    }

    static string Describe(MigrationCategory category) =>
        $"{category.Kind} {category.Order}: {category.Id}"
        + (category.CarriesBodies ? ", with bodies" : string.Empty)
        + (category.MustFollow is null ? string.Empty : $", after {category.MustFollow}");

    [Test]
    public void Every_category_runs_in_a_fixed_order()
    {
        var everyOptionalId = MigrationCategoryRegistry.All
            .Where(category => category.Kind == MigrationCategoryKind.Optional)
            .Select(category => category.Id)
            .ToArray();
        var engine = BuildEngine(everyOptionalId);

        var runOrder = engine.SelectCategories(MigrationCategoryKind.Required)
            .Concat(engine.SelectCategories(MigrationCategoryKind.Optional))
            .Select(Describe);

        Approver.Verify(string.Join(Environment.NewLine, runOrder));
    }

    [Test]
    public void Only_configured_optional_categories_are_selected()
    {
        string[] configured = [MigrationCategoryIds.GroupComments, MigrationCategoryIds.EventLog, MigrationCategoryIds.ArchivedAndResolvedFailedMessages];
        var engine = BuildEngine(configured);

        var selected = engine.SelectCategories(MigrationCategoryKind.Optional);
        var left = MigrationCategoryRegistry.All
            .Where(category => category.Kind == MigrationCategoryKind.Optional && !selected.Contains(category))
            .Select(category => category.Id);

        Approver.Verify(string.Join(Environment.NewLine,
            [
                $"Configured: {string.Join(", ", configured)}",
                "Runs as:",
                .. selected.Select(Describe),
                $"Never copied: {string.Join(", ", left)}"
            ]));
    }

    [Test]
    public async Task A_category_removed_from_configuration_leaves_its_checkpoint_row_untouched()
    {
        // Dropping a category from the configuration must not restart, reset or delete what it already
        // copied: those rows are in the target, and a row wound back to the start copies every one again.
        var stillConfigured = MigrationCategoryIds.CustomChecks;
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var source = new InMemoryMigrationSource();
        source.Seed(stillConfigured, new MigrationRow("check-1", new object(), new Dictionary<string, object>()));
        var target = new InMemoryMigrationTarget(checkpointStore);
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, [stillConfigured]);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);
        var previousRun = new MigrationCheckpoint(MigrationCategoryIds.EventLog, MigrationCategoryState.CompleteWithErrors, "cursor-99", 40, 2, 42, null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, null);
        await checkpointStore.Upsert(previousRun);

        var results = await engine.RunCategories(engine.SelectCategories(MigrationCategoryKind.Optional));

        var deselected = await checkpointStore.Read(MigrationCategoryIds.EventLog);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(results.Select(c => c.CategoryId), Is.EqualTo(new[] { stillConfigured }), "the run touched only the configured category");
            Assert.That(deselected, Is.EqualTo(previousRun with { Version = 1 }), "the row is still at the version the seeding save left it");
        }
    }
}
