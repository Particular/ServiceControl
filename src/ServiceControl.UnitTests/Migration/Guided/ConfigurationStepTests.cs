namespace ServiceControl.UnitTests.Migration.Guided;

using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Migration.Guided;

[TestFixture]
class ConfigurationStepTests
{
    [Test]
    public async Task The_summary_shows_every_value_and_where_it_came_from_before_anything_opens()
    {
        var harness = new GuidedMigrationHarness();
        harness.Source.Configuration = [new("Server", "https://raven:8080", "ServiceControl/RavenDB/ConnectionString"), new("Primary database", "servicecontrol", "default")];
        harness.Readiness.Configuration = [new("Connection string", "Host=pg;Password=*****", "ServiceControl/Database/ConnectionString")];

        var exitCode = await harness.Run(GuidedMigrationArguments.Prompting, "y", "n");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(harness.StepHeadings, Is.EqualTo(new[] { "1. Configuration" }));
            Assert.That(harness.Output, Does.Match(@"(?m)^\s+Source\s+RavenDB\r?$"));
            Assert.That(harness.Output, Does.Match(@"(?m)^\s+Server\s+https://raven:8080\s+\(ServiceControl/RavenDB/ConnectionString\)\r?$"));
            Assert.That(harness.Output, Does.Match(@"(?m)^\s+Primary database\s+servicecontrol\s+\(default\)\r?$"));
            Assert.That(harness.Output, Does.Match(@"(?m)^\s+Target\s+PostgreSQL\r?$"));
            Assert.That(harness.Output, Does.Match(@"(?m)^\s+Connection string\s+Host=pg;Password=\*{5}\s+\(ServiceControl/Database/ConnectionString\)\r?$"));
            Assert.That(harness.Output, Does.Contain("Is this correct? [y/N]").And.Contain("Nothing has been opened."));
            Assert.That((harness.Readiness.ChecksRequested, harness.Source.Opened), Is.EqualTo((0, false)), "a no opens nothing");
        }
    }
}
