namespace ServiceControl.MultiInstance.AcceptanceTests
{
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using Particular.Approvals;
    using TestSupport;
    using TestSupport.EndpointTemplates;
    using ServiceControl.AcceptanceTesting;
    using ServiceControlInstaller.Engine.Instances;

    [RunOnAllTransports]
    class PlatformConnectionTests : AcceptanceTest
    {
        [Test]
        public async Task ExposesConnectionDetails()
        {
            var config = await Define<MyContext>()
                .WithEndpoint<MyEndpoint>()
                .Done(async x =>
                {
                    var result = await this.GetRaw("/api/connection", ServiceControlInstanceName);
                    x.Connection = await result.Content.ReadAsStringAsync();
                    return true;
                })
                .Run();

            Assert.IsNotNull(config.Connection);

            var formatted =
                JsonConvert.SerializeObject(
                    JsonConvert.DeserializeObject(config.Connection),
                    Formatting.Indented
                );

            Approver.Verify(
                formatted,
                scenario: ScenarioName,
                scrubber: TransportIntegration.ScrubPlatformConnection
            );
        }

        string ScenarioName
        {
            get
            {
                switch (TransportIntegration.Name)
                {
                    case TransportNames.AmazonSQS:
                        return "SQS";
                    case TransportNames.AzureServiceBus:
                        return "ASB";
                    case TransportNames.AzureServiceBusEndpointOrientedTopologyDeprecated:
                    case TransportNames.AzureServiceBusEndpointOrientedTopologyLegacy:
                    case TransportNames.AzureServiceBusEndpointOrientedTopologyOld:
                    case TransportNames.AzureServiceBusForwardingTopologyDeprecated:
                    case TransportNames.AzureServiceBusForwardingTopologyLegacy:
                    case TransportNames.AzureServiceBusForwardingTopologyOld:
                        return "ASB.Old";
                    case TransportNames.AzureStorageQueue:
                        return "ASQ";
                    case TransportNames.MSMQ:
                        return "MSMQ";
                    case TransportNames.SQLServer:
                        return "SQL";
                    case TransportNames.RabbitMQConventionalRoutingTopology:
                    case TransportNames.RabbitMQDirectRoutingTopology:
                        return "RabbitMQ";
                    default:
                        return TransportIntegration.Name;
                }
            }
        }

        class MyEndpoint : EndpointConfigurationBuilder
        {
            public MyEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>();
            }
        }

        class MyContext : ScenarioContext
        {
            public string Connection { get; set; }
        }
    }
}
