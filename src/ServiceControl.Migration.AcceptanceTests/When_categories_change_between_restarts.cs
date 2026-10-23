namespace ServiceControl.Migration.AcceptanceTests;

using System.Threading.Tasks;
using NUnit.Framework;

[TestFixture]
[NonParallelizable]
class When_categories_change_between_restarts : MigrationAcceptanceTest
{
    [Test]
    public async Task Changing_the_windows_between_restarts_deletes_nothing()
    {
        await SeedSourceKnownEndpoints("Sales");
        await SeedSourceEndpointSettings(("Sales", true));

        SetSourceVariable("SERVICECONTROL_MIGRATION_EVENTLOGWINDOW", "0");
        await RunHostUntilTheApiAnswers();
        Assert.That(await ReadCheckpoint("EventLog"), Is.Null, "a category no run has copied has no row, and no column anywhere says it was selected");
        var firstRun = await ReadCheckpoint("EndpointSettings");

        SetSourceVariable("SERVICECONTROL_MIGRATION_EVENTLOGWINDOW", null);
        await RunHostUntilTheApiAnswers();
        Assert.That(await ReadCheckpoint("EventLog"), Is.Null, "the required copy runs required categories only, so turning an optional one on starts nothing here");

        SetSourceVariable("SERVICECONTROL_MIGRATION_EVENTLOGWINDOW", "0");
        await RunHostUntilTheApiAnswers();

        var endpointSettings = await ReadCheckpoint("EndpointSettings");

        Assert.Multiple(() =>
        {
            Assert.That(endpointSettings.CopiedCount, Is.EqualTo(1), "a finished category must not be copied again when the selection changes around it");
            Assert.That(endpointSettings.Cursor, Is.Not.Null, "changing the selection must not delete or reset what an earlier run recorded");
            Assert.That(endpointSettings.Version, Is.EqualTo(firstRun.Version), "a row saved again, even unchanged, would have moved its version");
            Assert.That(endpointSettings.StartedAt, Is.EqualTo(firstRun.StartedAt), "a copy run again from scratch would have a new start");
        });
    }
}
