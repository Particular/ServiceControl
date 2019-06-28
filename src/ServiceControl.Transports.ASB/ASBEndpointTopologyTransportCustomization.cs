﻿namespace ServiceControl.Transports.ASB
{
    using System;
    using NServiceBus;
    using NServiceBus.Raw;

    public class ASBEndpointTopologyTransportCustomization : TransportCustomization
    {
        public override void CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var remoteInstances = transportSettings.GetOrDefault<string[]>("TransportSettings.RemoteInstances") ?? new string[0];
            var remoteTypesToSubscribeTo = transportSettings.GetOrDefault<Type[]>("TransportSettings.RemoteTypesToSubscribeTo") ?? new Type[0];
            var endpointName = transportSettings.EndpointName;

            var transport = endpointConfig.UseTransport<AzureServiceBusTransport>();
            transport.Sanitization().UseStrategy<ValidateAndHashIfNeeded>();
            var topology = transport.UseEndpointOrientedTopology();
            topology.EnableMigrationToForwardingTopology();
            
            foreach (var remoteInstance in remoteInstances)
            {
                foreach (var remoteType in remoteTypesToSubscribeTo)
                {
                    topology.RegisterPublisher(remoteType, remoteInstance);
                }
            }

            foreach (var remoteType in remoteTypesToSubscribeTo)
            {
                topology.RegisterPublisher(remoteType, endpointName);
            }

            transport.ConfigureTransport(transportSettings);
        }

        public override void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<AzureServiceBusTransport>();
            transport.UseEndpointOrientedTopology();
            transport.ApplyHacksForNsbRaw();

            transport.ConfigureTransport(transportSettings);
        }
    }
}