namespace ServiceControl.Migration.Tests;

using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.ServiceControl.Hosting;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Hosting.Commands;

[TestFixture]
[NonParallelizable]
class MigrationSourceReportCommandTests
{
    [SetUp]
    public void SetUp()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_ERRORRETENTIONPERIOD", "10.00:00:00");
        Environment.SetEnvironmentVariable("SERVICECONTROL_RAVENDB_CONNECTIONSTRING", MigrationSourceServer.ServerUrl);
        Environment.SetEnvironmentVariable("SERVICECONTROL_RAVENDB_DATABASENAME", MigrationSourceServer.PrimaryDatabase);
        Environment.SetEnvironmentVariable("LICENSINGCOMPONENT_RAVENDB_THROUGHPUTDATABASENAME", MigrationSourceServer.ThroughputDatabase);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_ERRORRETENTIONPERIOD", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_RAVENDB_CONNECTIONSTRING", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_RAVENDB_DATABASENAME", null);
        Environment.SetEnvironmentVariable("LICENSINGCOMPONENT_RAVENDB_THROUGHPUTDATABASENAME", null);
    }

    [Test]
    public async Task The_report_names_the_databases_the_settings_and_the_collections()
    {
        var settings = new Settings(persisterType: "RavenDB", forwardErrorMessages: false, errorRetentionPeriod: TimeSpan.FromDays(10));

        var writer = new StringWriter();
        var original = Console.Out;
        Console.SetOut(writer);

        try
        {
            await new MigrationSourceReportCommand().Execute(new HostArguments([]), settings);
        }
        finally
        {
            Console.SetOut(original);
        }

        var report = writer.ToString();

        Assert.Multiple(() =>
        {
            Assert.That(report, Does.Contain("(external)"));
            Assert.That(report, Does.Contain(MigrationSourceServer.ServerUrl));
            Assert.That(report, Does.Contain("from ServiceControl/RavenDB/DatabaseName"));
            Assert.That(report, Does.Contain("from LicensingComponent/RavenDB/ThroughputDatabaseName"));
            Assert.That(report, Does.Match(@"EndpointSettings\s+1"), "The report has to render a count, not just name the collection.");
        });
    }
}
