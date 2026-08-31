namespace ServiceControl.Persistence.Tests.SqlServer;

using NUnit.Framework;
using ServiceControl.Persistence.EFCore.SqlServer;

// EngineEdition values per
// https://learn.microsoft.com/en-us/sql/t-sql/functions/serverproperty-transact-sql
[TestFixture]
class SqlServerDatabaseHostingProbeTests
{
    [TestCase(5, "AzureSql")]
    [TestCase(8, "AzureSqlManagedInstance")]
    [TestCase(9, "AzureSqlEdge")]
    public void Should_take_the_azure_service_from_the_engine_edition(int engineEdition, string expected) =>
        Assert.That(SqlServerDatabaseHostingProbe.HostingFor(engineEdition, "anything.example.com"), Is.EqualTo(expected),
            "The engine names its own service, so the host name must not get a say");

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void Should_report_an_ordinary_edition_on_an_unrecognised_host_as_self_hosted(int engineEdition) =>
        Assert.That(SqlServerDatabaseHostingProbe.HostingFor(engineEdition, "db01.corp.example"), Is.EqualTo("SelfHosted"),
            "The server answered and is not an Azure service, which is evidence rather than absence of it");

    [TestCase(2)]
    [TestCase(3)]
    public void Should_still_recognise_rds_running_an_ordinary_edition(int engineEdition) =>
        Assert.That(SqlServerDatabaseHostingProbe.HostingFor(engineEdition, "sc.abcdef.eu-west-1.rds.amazonaws.com"), Is.EqualTo("AwsRds"),
            "RDS for SQL Server reports Standard or Enterprise, so the host name is the only thing that gives it away");

    // Synapse (6, 11) and Fabric (12) are deliberately unmapped, so they take this path.
    [TestCase(6)]
    [TestCase(11)]
    [TestCase(12)]
    [TestCase(99)]
    public void Should_fall_back_to_the_host_for_an_unmapped_edition(int engineEdition) =>
        Assert.That(SqlServerDatabaseHostingProbe.HostingFor(engineEdition, "sc.database.windows.net"), Is.EqualTo("AzureSql"));

    [TestCase(6)]
    [TestCase(11)]
    [TestCase(12)]
    [TestCase(99)]
    public void Should_report_unknown_for_an_unmapped_edition_on_an_unrecognised_host(int engineEdition) =>
        Assert.That(SqlServerDatabaseHostingProbe.HostingFor(engineEdition, "db01.corp.example"), Is.EqualTo("Unknown"),
            "An edition we do not map is not evidence of an ordinary SQL Server");
}
