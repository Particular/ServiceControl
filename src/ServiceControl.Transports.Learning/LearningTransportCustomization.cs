﻿namespace ServiceControl.Transports.Learning
{
    using NServiceBus;
    using NServiceBus.Raw;
    using ServiceControl.Transports.LearningTransport;

    public class LearningTransportCustomization : TransportCustomization
    {
        public override void CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<LearningTransport>();
            transport.StorageDirectory(transportSettings.ConnectionString);
            transport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
        }

        public override void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<LearningTransport>();
            transport.StorageDirectory(transportSettings.ConnectionString);
            transport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
        }

        public override IProvideQueueLength CreateQueueLengthProvider()
        {
            return new QueueLengthProvider();
        }
    }
}