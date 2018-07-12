namespace ServiceControl.Transports.ASB
{
    using System;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Raw;
    using ServiceControl.Infrastructure.Transport;

    public class ASBEndpointTopologyTransportCustomization : TransportCustomization
    {
        public override void CustomizeEndpoint(EndpointConfiguration endpointConfig, string connectionString)
        {
            endpointConfig.UseSerialization<NewtonsoftSerializer>();
            
            var remoteInstances = endpointConfig.GetSettings().Get<string[]>("ServiceControl.RemoteInstances");
            var remoteTypesToSubscribeTo = endpointConfig.GetSettings().Get<Type[]>("ServiceControl.RemoteTypesToSubscribeTo");
            var endpointName = endpointConfig.GetSettings().Get<string>("ServiceControl.EndpointName");
            
            var transport = endpointConfig.UseTransport<AzureServiceBusTransport>();
            transport.Sanitization().UseStrategy<ValidateAndHashIfNeeded>();
            var topology = transport.UseEndpointOrientedTopology();
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

            ConfigureTransport(transport, connectionString);
            CustomizeEndpointTransport(transport);
        }

        public override void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, string connectionString)
        {
            var transport = endpointConfig.UseTransport<AzureServiceBusTransport>();
            transport.UseEndpointOrientedTopology();
            transport.ApplyHacksForNsbRaw();
            ConfigureTransport(transport, connectionString);
            CustomizeRawEndpointTransport(transport);
        }
        
        protected virtual void CustomizeEndpointTransport(TransportExtensions<AzureServiceBusTransport> extensions)
        {
        }

        protected virtual void CustomizeRawEndpointTransport(TransportExtensions<AzureServiceBusTransport> extensions)
        {
        }
        
        static void ConfigureTransport(TransportExtensions<AzureServiceBusTransport> transport, string connectionString)
        {
            transport.Transactions(TransportTransactionMode.ReceiveOnly);
            transport.ConnectionString(connectionString);
            transport.NumberOfClientsPerEntity(1);
        }
    }
}