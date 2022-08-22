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
            configuration.UseSerialization<NewtonsoftJsonSerializer>();

#pragma warning disable 618
            var transportConfig = configuration.UseTransport<AzureServiceBusTransport>();
#pragma warning restore 618
            transportConfig.UseForwardingTopology();

            transportConfig.ConnectionString(ConnectionString);

            transportConfig.Sanitization().UseStrategy<ValidateAndHashIfNeeded>();

            return Task.FromResult(0);
        }

        public Task Cleanup()
        {
            return Task.FromResult(0);
        }

        public string Name => TransportNames.AzureServiceBusForwardingTopologyDeprecated;

        public string TypeName => $"{typeof(ServiceControl.Transports.ASB.ASBForwardingTopologyTransportCustomization).AssemblyQualifiedName}";

        public string ConnectionString { get; set; }
        public string ScrubPlatformConnection(string input) => input;
    }
}