namespace ServiceControl.Transport.Tests;

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Transports;
using Transports.PostgreSql;

[TestFixture]
class EnvironmentDataTests
{
    [TestCase("Host=localhost;Username=nsb;Password=contoso-secret", "Password")]
    [TestCase("Host=localhost;Username=nsb;Passfile=/home/nsb/.pgpass", "Password")]
    [TestCase("Host=localhost;Username=nsb;SSL Certificate=/certs/client.crt", "ClientCertificate")]
    [TestCase("Host=localhost;Username=nsb", "None")]
    public void Should_report_the_authentication_mode(string connectionString, string expected) =>
        Assert.That(Read(connectionString)["Transport.Auth"], Is.EqualTo(expected));

    [Test]
    public void Should_report_default_schema_and_subscriptions_table()
    {
        var data = Read("Host=localhost;Username=nsb;Password=contoso-secret");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["Transport.PostgreSQL.QueueSchema"], Is.EqualTo("Default"));
            Assert.That(data["Transport.PostgreSQL.SubscriptionsTable"], Is.EqualTo("Default"));
            Assert.That(data["Transport.CertificateValidation"], Is.EqualTo("Default"));
        }
    }

    [Test]
    public void Should_report_a_custom_schema_and_subscriptions_table_without_naming_them()
    {
        var data = Read("Host=localhost;Username=nsb;Password=contoso-secret;Queue Schema=contoso;Subscriptions Table=contoso.subscriptions");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["Transport.PostgreSQL.QueueSchema"], Is.EqualTo("Custom"));
            Assert.That(data["Transport.PostgreSQL.SubscriptionsTable"], Is.EqualTo("Custom"));
            Assert.That(string.Join("|", data.Values), Does.Not.Contain("contoso").IgnoreCase);
        }
    }

    static Dictionary<string, string> Read(string connectionString) =>
        new PostgreSqlTransportCustomization()
            .GetEnvironmentData(new TransportSettings { ConnectionString = connectionString })
            .ToDictionary(datum => datum.Key, datum => datum.ReadValue());
}
