namespace ServiceControl.AcceptanceTesting.InfrastructureConfig
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using ServiceControl.AcceptanceTesting;
    using ServiceControlInstaller.Engine.Instances;

    public class ConfigureEndpointAzureServiceBusEndpointTopologyTransport : ITransportIntegration
    {
        public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
        {
            configuration.UseSerialization<NewtonsoftJsonSerializer>();

#pragma warning disable 618
            var transportConfig = configuration.UseTransport<AzureServiceBusTransport>();
#pragma warning restore 618

            transportConfig.ConnectionString(ConnectionString);

            var endpointOrientedTopology = transportConfig.UseEndpointOrientedTopology();
            foreach (var publisher in publisherMetadata.Publishers)
            {
                foreach (var eventType in publisher.Events)
                {
                    endpointOrientedTopology.RegisterPublisher(eventType, publisher.PublisherName);
                }
            }

            transportConfig.Sanitization().UseStrategy<ValidateAndHashIfNeeded>();

            return Task.FromResult(0);
        }

        public Task Cleanup()
        {
            return Task.FromResult(0);
        }

        public string Name => TransportNames.AzureServiceBusEndpointOrientedTopologyDeprecated;
        public string TypeName => $"{typeof(ServiceControl.Transports.ASB.ASBEndpointTopologyTransportCustomization).AssemblyQualifiedName}";
        public string ConnectionString { get; set; }
        public string ScrubPlatformConnection(string input) => input;
    }
}