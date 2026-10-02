namespace ServiceControl.Transports.UnitTests.ASBS
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using ServiceControl.Transports.ASBS;

    [TestFixture]
    [NonParallelizable]
    class EnvironmentDataTests
    {
        [TearDown]
        public void TearDown() => Environment.SetEnvironmentVariable(TopologyVariable, null);

        [TestCase("Endpoint=sb://contoso.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=c2VjcmV0", "SharedAccessKey")]
        [TestCase("Endpoint=sb://contoso.servicebus.windows.net/;Authentication=Managed Identity", "ManagedIdentity")]
        [TestCase("Endpoint=sb://contoso.servicebus.windows.net/;Authentication=Managed Identity;ClientId=11111111-2222-3333-4444-555555555555", "ManagedIdentity")]
        [TestCase("contoso.servicebus.windows.net", "DefaultAzureCredential")]
        public void Should_report_the_authentication_mode(string connectionString, string expected) =>
            Assert.That(Read(connectionString)["Transport.Auth"], Is.EqualTo(expected));

        [Test]
        public void Should_report_defaults_when_the_connection_string_selects_no_options()
        {
            var data = Read("Endpoint=sb://contoso.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=c2VjcmV0");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(data["Transport.CertificateValidation"], Is.EqualTo("Default"));
                Assert.That(data["Transport.AzureServiceBus.Topology"], Is.EqualTo("TopicPerEvent"));
                Assert.That(data["Transport.AzureServiceBus.Partitioning"], Is.EqualTo("Disabled"));
                Assert.That(data["Transport.AzureServiceBus.WebSockets"], Is.EqualTo("Disabled"));
                Assert.That(data["Transport.AzureServiceBus.HierarchyNamespace"], Is.EqualTo("None"));
            }
        }

        [Test]
        public void Should_report_selected_options_without_their_values()
        {
            var data = Read("Endpoint=sb://contoso.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=c2VjcmV0;TopicName=contoso-bundle;EnablePartitioning=true;TransportType=AmqpWebSockets;HierarchyNamespace=contoso-prod");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(data["Transport.AzureServiceBus.Topology"], Is.EqualTo("Migration"));
                Assert.That(data["Transport.AzureServiceBus.Partitioning"], Is.EqualTo("Enabled"));
                Assert.That(data["Transport.AzureServiceBus.WebSockets"], Is.EqualTo("Enabled"));
                Assert.That(data["Transport.AzureServiceBus.HierarchyNamespace"], Is.EqualTo("Configured"));
                Assert.That(string.Join("|", data.Values), Does.Not.Contain("contoso").IgnoreCase.And.Not.Contain("c2VjcmV0"));
            }
        }

        [Test]
        public void Should_report_a_custom_topology()
        {
            Environment.SetEnvironmentVariable(TopologyVariable, "{}");

            var data = Read("Endpoint=sb://contoso.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=c2VjcmV0");

            Assert.That(data["Transport.AzureServiceBus.Topology"], Is.EqualTo("Custom"));
        }

        static Dictionary<string, string> Read(string connectionString) =>
            new ASBSTransportCustomization()
                .GetEnvironmentData(new TransportSettings { ConnectionString = connectionString })
                .ToDictionary(datum => datum.Key, datum => datum.ReadValue());

        const string TopologyVariable = "SERVICECONTROL_TRANSPORT_ASBS_TOPOLOGY";
    }
}
