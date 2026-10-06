namespace ServiceControl.Transport.Tests;

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Transports;
using Transports.SqlServer;

[TestFixture]
class EnvironmentDataTests
{
    [Test]
    public void Should_report_default_schema_and_subscriptions_table()
    {
        var data = Read("Server=.;Database=nsb;Integrated Security=true");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["Transport.SQLServer.QueueSchema"], Is.EqualTo("Default"));
            Assert.That(data["Transport.SQLServer.SubscriptionsTable"], Is.EqualTo("Default"));
        }
    }

    [Test]
    public void Should_report_a_custom_schema_and_subscriptions_table_without_naming_them()
    {
        var data = Read("Server=.;Database=nsb;User Id=sa;Password=contoso-secret;Queue Schema=contoso;Subscriptions Table=contoso.subscriptions");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["Transport.SQLServer.QueueSchema"], Is.EqualTo("Custom"));
            Assert.That(data["Transport.SQLServer.SubscriptionsTable"], Is.EqualTo("Custom"));
            Assert.That(string.Join("|", data.Values), Does.Not.Contain("contoso").IgnoreCase);
        }
    }

    [TestCase(3, "16.0.4225.2")]
    [TestCase(8, "16.0.4165.4")]
    public void Should_report_only_the_major_sql_version(int engineEdition, string productVersion) =>
        Assert.That(DatabaseDetails.VersionFor(engineEdition, productVersion), Is.EqualTo("16"));

    [Test]
    public void Should_report_azure_sql_database_by_name_rather_than_by_version() =>
        Assert.That(DatabaseDetails.VersionFor(5, "12.0.2000.8"), Is.EqualTo("AzureSql"),
            "Azure SQL Database always reports 12, which would read as SQL Server 2014");

    static Dictionary<string, string> Read(string connectionString) =>
        new SqlServerTransportCustomization()
            .GetEnvironmentData(new TransportSettings { ConnectionString = connectionString })
            .ToDictionary(datum => datum.Key, datum => datum.ReadValue());
}
