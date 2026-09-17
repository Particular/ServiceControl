namespace ServiceControl.UnitTests.Migration;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using Particular.Approvals;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.UnitTests.Migration.Fakes;

[TestFixture]
class MigrationEngineCategorySelectionTests
{
    static MigrationEngine BuildEngine(IReadOnlyCollection<string> selectedOptionalIds, out InMemoryMigrationCheckpointStore checkpointStore)
    {
        checkpointStore = new InMemoryMigrationCheckpointStore();
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
        var engine = BuildEngine(everyOptionalId, out _);

        var runOrder = engine.SelectCategories(MigrationCategoryKind.Required)
            .Concat(engine.SelectCategories(MigrationCategoryKind.Optional))
            .Select(Describe);

        Approver.Verify(string.Join(Environment.NewLine, runOrder));
    }

    [Test]
    public void Only_configured_optional_categories_are_selected()
    {
        string[] configured = [MigrationCategoryIds.GroupComments, MigrationCategoryIds.EventLog, MigrationCategoryIds.ArchivedAndResolvedFailedMessages];
        var engine = BuildEngine(configured, out _);

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
    public void A_category_removed_from_configuration_leaves_its_checkpoint_row_untouched()
    {
        var engine = BuildEngine([], out var checkpointStore);
        var previousRun = new MigrationCheckpoint("EventLog", MigrationCategoryState.CompleteWithErrors, "cursor-99", 40, 2, 42, null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, null);
        checkpointStore.Upsert(previousRun).GetAwaiter().GetResult();

        var selected = engine.SelectCategories(MigrationCategoryKind.Optional);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(selected.Select(c => c.Id), Does.Not.Contain("EventLog"));
            Assert.That(checkpointStore.Read("EventLog").GetAwaiter().GetResult(), Is.EqualTo(previousRun with { Version = 1 }));
        }
    }
}
