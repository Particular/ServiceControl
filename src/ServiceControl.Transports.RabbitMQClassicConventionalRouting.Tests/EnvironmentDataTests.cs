namespace ServiceControl.Transport.Tests;

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Transports;
using Transports.RabbitMQ;

[TestFixture]
class EnvironmentDataTests
{
    [TestCase("host=localhost")]
    [TestCase("amqp://guest:guest@localhost:5672")]
    public void Should_report_defaults_when_the_connection_string_selects_no_options(string connectionString)
    {
        var data = Read(new RabbitMQClassicConventionalRoutingTransportCustomization(), connectionString);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["Transport.Auth"], Is.EqualTo("Password"));
            Assert.That(data["Transport.CertificateValidation"], Is.EqualTo("Default"));
            Assert.That(data["Transport.RabbitMQ.DeliveryLimitValidation"], Is.EqualTo("Enabled"));
            Assert.That(data["Transport.RabbitMQ.ManagementApi"], Is.EqualTo("Default"));
        }
    }

    [Test]
    public void Should_report_selected_options_on_either_routing_topology_without_their_values()
    {
        const string connectionString = "host=rabbit.contoso.local;UseExternalAuthMechanism=true;DisableRemoteCertificateValidation=true;ValidateDeliveryLimits=false;ManagementApiUrl=https://rabbit.contoso.local:15671";

        foreach (var customization in new ITransportCustomization[] { new RabbitMQClassicConventionalRoutingTransportCustomization(), new RabbitMQQuorumDirectRoutingTransportCustomization() })
        {
            var data = Read(customization, connectionString);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(data["Transport.Auth"], Is.EqualTo("ExternalCertificate"));
                Assert.That(data["Transport.CertificateValidation"], Is.EqualTo("Relaxed"));
                Assert.That(data["Transport.RabbitMQ.DeliveryLimitValidation"], Is.EqualTo("Disabled"));
                Assert.That(data["Transport.RabbitMQ.ManagementApi"], Is.EqualTo("Configured"));
                Assert.That(string.Join("|", data.Values), Does.Not.Contain("contoso").IgnoreCase);
            }
        }
    }

    static Dictionary<string, string> Read(ITransportCustomization customization, string connectionString) =>
        customization
            .GetEnvironmentData(new TransportSettings { ConnectionString = connectionString })
            .ToDictionary(datum => datum.Key, datum => datum.ReadValue());
}
