namespace ServiceControl.UnitTests.Migration;

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Migration.Checks;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
[NonParallelizable]
class SelectedCategoriesAreCoherentCheckTests
{
    [TearDown]
    public void ClearOptionalCategories() => Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_OPTIONALCATEGORIES", null);

    [Test]
    public async Task No_optional_categories_selected_passes_and_selects_none()
    {
        var check = new SelectedCategoriesAreCoherentCheck();

        await check.Run();

        Assert.That(check.Options.SelectedOptionalCategoryIds, Is.Empty);
    }

    [Test]
    public async Task The_selected_ids_are_exposed_and_the_spacing_is_tolerated()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_OPTIONALCATEGORIES", "EventLog, CustomChecks");
        var check = new SelectedCategoriesAreCoherentCheck();

        await check.Run();

        Assert.That(check.Options.SelectedOptionalCategoryIds, Is.EquivalentTo(new[] { MigrationCategoryIds.EventLog, MigrationCategoryIds.CustomChecks }));
    }

    [Test]
    public void An_unknown_optional_category_is_refused_and_the_typo_is_named()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_OPTIONALCATEGORIES", "EventLogs");

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new SelectedCategoriesAreCoherentCheck().Run());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Message, Does.Contain("EventLogs"));
            Assert.That(exception.Message, Does.Contain(MigrationSettings.OptionalCategoriesKey));
        }
    }
}
