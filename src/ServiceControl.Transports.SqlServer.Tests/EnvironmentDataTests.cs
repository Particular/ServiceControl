namespace ServiceControl.Transport.Tests;

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Transports;
using Transports.SqlServer;

[TestFixture]
class EnvironmentDataTests
{
    [TestCase("Server=.;Database=nsb;Integrated Security=true", "Integrated")]
    [TestCase("Server=.;Database=nsb;User Id=sa;Password=contoso-secret", "SqlPassword")]
    [TestCase("Server=tcp:contoso.database.windows.net;Database=nsb;Authentication=Active Directory Managed Identity", "EntraId")]
    [TestCase("Server=tcp:contoso.database.windows.net;Database=nsb;Authentication=Active Directory Default", "EntraId")]
    public void Should_report_the_authentication_mode(string connectionString, string expected) =>
        Assert.That(Read(connectionString)["Transport.Auth"], Is.EqualTo(expected));

    [Test]
    public void Should_report_default_schema_and_subscriptions_table()
    {
        var data = Read("Server=.;Database=nsb;Integrated Security=true");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["Transport.SQLServer.QueueSchema"], Is.EqualTo("Default"));
            Assert.That(data["Transport.SQLServer.SubscriptionsTable"], Is.EqualTo("Default"));
            Assert.That(data["Transport.CertificateValidation"], Is.EqualTo("Default"));
        }
    }

    [Test]
    public void Should_report_a_custom_schema_and_subscriptions_table_without_naming_them()
    {
        var data = Read("Server=.;Database=nsb;User Id=sa;Password=contoso-secret;Queue Schema=contoso;Subscriptions Table=contoso.subscriptions");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["Transport.Auth"], Is.EqualTo("SqlPassword"));
            Assert.That(data["Transport.SQLServer.QueueSchema"], Is.EqualTo("Custom"));
            Assert.That(data["Transport.SQLServer.SubscriptionsTable"], Is.EqualTo("Custom"));
            Assert.That(string.Join("|", data.Values), Does.Not.Contain("contoso").IgnoreCase);
        }
    }

    static Dictionary<string, string> Read(string connectionString) =>
        new SqlServerTransportCustomization()
            .GetEnvironmentData(new TransportSettings { ConnectionString = connectionString })
            .ToDictionary(datum => datum.Key, datum => datum.ReadValue());
}
