namespace ServiceControl.Persistence.Tests.PostgreSql;

using NUnit.Framework;
using ServiceControl.Persistence.EFCore.PostgreSql;

[TestFixture]
class PostgreSqlDatabaseHostingProbeTests
{
    [Test]
    public void Should_report_azure_when_the_azure_admin_role_exists() =>
        Assert.That(Hosting(azure: true), Is.EqualTo("AzurePostgres"));

    [Test]
    public void Should_report_rds_when_the_rds_superuser_role_exists() =>
        Assert.That(Hosting(rds: true), Is.EqualTo("AwsRds"));

    [Test]
    public void Should_report_cloud_sql_when_the_cloud_sql_superuser_role_exists() =>
        Assert.That(Hosting(cloudSql: true), Is.EqualTo("GoogleCloudSql"));

    [Test]
    public void Should_report_self_hosted_when_no_managed_role_exists() =>
        Assert.That(PostgreSqlDatabaseHostingProbe.HostingFor(false, false, false, "db01.corp.example"), Is.EqualTo("SelfHosted"),
            "The server answered and has no managed fingerprint, which is evidence rather than absence of it");

    [Test]
    public void Should_prefer_the_host_when_a_managed_service_creates_no_role() =>
        Assert.That(PostgreSqlDatabaseHostingProbe.HostingFor(false, false, false, "sc.abcdef.eu-west-1.rds.amazonaws.com"), Is.EqualTo("AwsRds"));

    static string Hosting(bool azure = false, bool rds = false, bool cloudSql = false) =>
        PostgreSqlDatabaseHostingProbe.HostingFor(azure, rds, cloudSql, "anything.example.com");
}
