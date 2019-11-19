﻿using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using ServiceControl.AcceptanceTesting;
using ServiceControlInstaller.Engine.Instances;

namespace ServiceControl.AcceptanceTesting.InfrastructureConfig
{
    public class ConfigureEndpointAzureServiceBusEndpointTopologyTransport : ITransportIntegration
    {
        public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
        {
            configuration.UseSerialization<NewtonsoftSerializer>();

            var transportConfig = configuration.UseTransport<AzureServiceBusTransport>();

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

        public string Name => TransportNames.AzureServiceBusEndpointOrientedTopology;
        public string TypeName => $"{typeof(ServiceControl.Transports.ASB.ASBEndpointTopologyTransportCustomization).AssemblyQualifiedName}";
        public string ConnectionString { get; set; }
    }
}