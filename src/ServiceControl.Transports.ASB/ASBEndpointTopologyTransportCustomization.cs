namespace ServiceControl.Transports.ASB
{
    using System;
    using NServiceBus;
    using NServiceBus.Raw;

    public class ASBEndpointTopologyTransportCustomization : TransportCustomization
    {
        public override void CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var remoteInstances = transportSettings.Get<string[]>("TransportSettings.RemoteInstances");
            var remoteTypesToSubscribeTo = transportSettings.Get<Type[]>("TransportSettings.RemoteTypesToSubscribeTo");
            var endpointName = transportSettings.EndpointName;

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

            ConfigureTransport(transport, transportSettings);
        }

        public override void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<AzureServiceBusTransport>();
            transport.UseEndpointOrientedTopology();
            transport.ApplyHacksForNsbRaw();
            ConfigureTransport(transport, transportSettings);
        }

        static void ConfigureTransport(TransportExtensions<AzureServiceBusTransport> transport, TransportSettings transportSettings)
        {
            transport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
            transport.ConnectionString(transportSettings.ConnectionString);

            transport.MessageReceivers().PrefetchCount(0);
            transport.Queues().LockDuration(TimeSpan.FromMinutes(5));
            transport.Subscriptions().LockDuration(TimeSpan.FromMinutes(5));
            transport.MessagingFactories().NumberOfMessagingFactoriesPerNamespace(2);
            transport.NumberOfClientsPerEntity(Math.Min(Environment.ProcessorCount, transportSettings.MaxConcurrency));
        }
    }
}