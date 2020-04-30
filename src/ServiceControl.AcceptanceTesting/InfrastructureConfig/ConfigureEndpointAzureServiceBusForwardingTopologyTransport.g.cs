namespace ServiceControl.AcceptanceTesting.InfrastructureConfig
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using ServiceControl.AcceptanceTesting;
    using ServiceControlInstaller.Engine.Instances;

    public class ConfigureEndpointAzureServiceBusForwardingTopologyTransport : ITransportIntegration
    {
        public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
        {
            configuration.UseSerialization<NewtonsoftSerializer>();

#pragma warning disable CS0618 // Type or member is obsolete
            var transportConfig = configuration.UseTransport<AzureServiceBusTransport>();
#pragma warning restore CS0618 // Type or member is obsolete
            transportConfig.UseForwardingTopology();

            transportConfig.ConnectionString(ConnectionString);

            transportConfig.Sanitization().UseStrategy<ValidateAndHashIfNeeded>();

            return Task.FromResult(0);
        }

        public Task Cleanup()
        {
            return Task.FromResult(0);
        }

        public string Name => TransportNames.AzureServiceBusForwardingTopology;

        public string TypeName => $"{typeof(ServiceControl.Transports.ASB.ASBForwardingTopologyTransportCustomization).AssemblyQualifiedName}";

        public string ConnectionString { get; set; }
    }
}