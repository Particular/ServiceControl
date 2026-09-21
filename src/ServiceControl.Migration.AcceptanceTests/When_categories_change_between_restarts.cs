namespace ServiceControl.Migration.AcceptanceTests;

using System.Threading.Tasks;
using NUnit.Framework;

[TestFixture]
// Mandatory, not stylistic: this assembly is Parallelizable(ParallelScope.All) and these fixtures set
// process-global environment variables. One fixture added without it makes the whole suite intermittent.
[NonParallelizable]
class When_categories_change_between_restarts : MigrationAcceptanceTest
{
    [Test]
    public async Task Changing_the_selection_between_restarts_deletes_nothing()
    {
        await SeedSourceKnownEndpoints("Sales");
        await SeedSourceEndpointSettings(("Sales", true));

        await RunHostUntilTheApiAnswers();
        Assert.That(await ReadCheckpoint("EventLog"), Is.Null, "a category no run has copied has no row, and no column anywhere says it was selected");

        SetSourceVariable("SERVICECONTROL_MIGRATION_OPTIONALCATEGORIES", "EventLog");
        await RunHostUntilTheApiAnswers();
        Assert.That(await ReadCheckpoint("EventLog"), Is.Null, "the required copy runs required categories only, so selecting an optional one starts nothing here");

        SetSourceVariable("SERVICECONTROL_MIGRATION_OPTIONALCATEGORIES", string.Empty);
        await RunHostUntilTheApiAnswers();

        var endpointSettings = await ReadCheckpoint("EndpointSettings");

        Assert.Multiple(() =>
        {
            Assert.That(endpointSettings.CopiedCount, Is.EqualTo(1), "a finished category must not be copied again when the selection changes around it");
            Assert.That(endpointSettings.Cursor, Is.Not.Null, "changing the selection must not delete or reset what an earlier run recorded");
        });
    }
}
