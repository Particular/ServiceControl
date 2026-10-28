namespace ServiceControl.Persistence.Tests.SqlServer;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Abstractions;
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
        Assert.That(SqlServerDatabaseHostingProbe.HostingFor(engineEdition, false, "anything.example.com"), Is.EqualTo(expected),
            "The engine names its own service, so the host name must not get a say");

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void Should_report_an_ordinary_edition_on_an_unrecognised_host_as_self_hosted(int engineEdition) =>
        Assert.That(SqlServerDatabaseHostingProbe.HostingFor(engineEdition, false, "db01.corp.example"), Is.EqualTo("SelfHosted"),
            "The server answered and is not an Azure service, which is evidence rather than absence of it");

    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void Should_recognise_rds_by_its_rdsadmin_database_whatever_the_host_name(int engineEdition) =>
        Assert.That(SqlServerDatabaseHostingProbe.HostingFor(engineEdition, true, "db01.corp.example"), Is.EqualTo("AwsRds"),
            "RDS for SQL Server reports an ordinary edition, and a customer's own DNS name must not make it look self-hosted");

    [TestCase(2)]
    [TestCase(3)]
    public void Should_still_recognise_rds_by_its_host_name_when_rdsadmin_is_not_visible(int engineEdition) =>
        Assert.That(SqlServerDatabaseHostingProbe.HostingFor(engineEdition, false, "sc.abcdef.eu-west-1.rds.amazonaws.com"), Is.EqualTo("AwsRds"));

    // Synapse (6, 11) and Fabric (12) are deliberately unmapped, so they take this path.
    [TestCase(6)]
    [TestCase(11)]
    [TestCase(12)]
    [TestCase(99)]
    public void Should_fall_back_to_the_host_for_an_unmapped_edition(int engineEdition) =>
        Assert.That(SqlServerDatabaseHostingProbe.HostingFor(engineEdition, false, "sc.database.windows.net"), Is.EqualTo("AzureSql"));

    [TestCase(6)]
    [TestCase(11)]
    [TestCase(12)]
    [TestCase(99)]
    public void Should_report_unknown_for_an_unmapped_edition_on_an_unrecognised_host(int engineEdition) =>
        Assert.That(SqlServerDatabaseHostingProbe.HostingFor(engineEdition, false, "db01.corp.example"), Is.EqualTo("Unknown"),
            "An edition we do not map is not evidence of an ordinary SQL Server");

    [TestCase(2, "Standard")]
    [TestCase(3, "Enterprise")]
    [TestCase(4, "Express")]
    [TestCase(5, "NotApplicable")]
    [TestCase(8, "NotApplicable")]
    [TestCase(1, "Other")]
    [TestCase(9, "Other")]
    [TestCase(12, "Other")]
    public void Should_report_the_edition_family(int engineEdition, string expected) =>
        Assert.That(SqlServerDatabaseHostingProbe.EditionFamily(engineEdition), Is.EqualTo(expected));

    [TestCase(null, "NotApplicable")]
    [TestCase("Basic", "Basic")]
    [TestCase("S0", "Standard")]
    [TestCase("S12", "Standard")]
    [TestCase("P15", "Premium")]
    [TestCase("GP_Gen5_2", "GeneralPurpose")]
    [TestCase("GP_S_Gen5_1", "GeneralPurpose")]
    [TestCase("GP_DC_8", "GeneralPurpose")]
    [TestCase("BC_Gen5_8", "BusinessCritical")]
    [TestCase("HS_Gen5_4", "Hyperscale")]
    [TestCase("ElasticPool", "ElasticPool")]
    [TestCase("DW100c", "Other")]
    [TestCase("System2", "Other")]
    public void Should_report_only_the_service_objective_tier(string serviceObjective, string expected) =>
        Assert.That(SqlServerDatabaseHostingProbe.ServiceObjectiveTier(serviceObjective), Is.EqualTo(expected));

    [TestCase("Server=sc.database.windows.net;Database=sc")]
    [TestCase("not a connection string")]
    public async Task Should_report_edition_and_service_objective_as_unknown_when_the_server_cannot_be_asked(string connectionString)
    {
        var probe = new SqlServerDatabaseHostingProbe(new SqlServerPersisterSettings { ConnectionString = connectionString, BodyStorage = new FileSystemBodyStorageSettings { StoragePath = "/var/bodies" } }, new UnreachableScopeFactory(), NullLogger<SqlServerDatabaseHostingProbe>.Instance);

        var hosting = await probe.Probe();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(hosting.ServerEdition, Is.EqualTo("Unknown"));
            Assert.That(hosting.ServiceObjective, Is.EqualTo("Unknown"));
        }
    }

    sealed class UnreachableScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new InvalidOperationException("The database cannot be reached");
    }
}
