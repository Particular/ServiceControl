namespace ServiceControl.UnitTests.Migration;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
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

    [Test]
    public void All_twelve_required_categories_are_always_selected()
    {
        var engine = BuildEngine([], out _);

        Assert.That(engine.SelectCategories(MigrationCategoryKind.Required), Has.Count.EqualTo(12));
    }

    [Test]
    public void Only_configured_optional_categories_are_selected()
    {
        var engine = BuildEngine(["EventLog"], out _);

        var selected = engine.SelectCategories(MigrationCategoryKind.Optional);

        Assert.That(selected.Select(c => c.Id), Is.EqualTo(new[] { "EventLog" }));
    }

    [Test]
    public void A_category_removed_from_configuration_leaves_its_checkpoint_row_untouched()
    {
        var engine = BuildEngine([], out var checkpointStore);
        var previousRun = new MigrationCheckpoint("EventLog", Selected: true, MigrationCategoryState.CompleteWithErrors, "cursor-99", 40, 2, 42, null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, null, null);
        checkpointStore.Upsert(previousRun).GetAwaiter().GetResult();

        var selected = engine.SelectCategories(MigrationCategoryKind.Optional);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(selected.Select(c => c.Id), Does.Not.Contain("EventLog"));
            Assert.That(checkpointStore.Read("EventLog").GetAwaiter().GetResult(), Is.EqualTo(previousRun));
        }
    }

    [Test]
    public void A_category_added_to_configuration_is_selected_on_the_next_run()
    {
        var engine = BuildEngine(["CustomChecks"], out _);

        Assert.That(engine.SelectCategories(MigrationCategoryKind.Optional).Select(c => c.Id), Is.EqualTo(new[] { "CustomChecks" }));
    }
}
