namespace ServiceControl.Transports.ASBS
{
    using System;
    using System.Data.Common;
    using Azure.Messaging.ServiceBus;
    using NServiceBus;
    using NServiceBus.Raw;

    public class ASBSTransportCustomization : TransportCustomization
    {
        public override void CustomizeForAuditIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = endpointConfiguration.UseTransport<AzureServiceBusTransport>();

            CustomizeEndpoint(transport, transportSettings);

            transport.ConfigureTransport(transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        public override void CustomizeForMonitoringIngestion(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = endpointConfiguration.UseTransport<AzureServiceBusTransport>();

            CustomizeEndpoint(transport, transportSettings);

            transport.ConfigureTransport(transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        public override void CustomizeForReturnToSenderIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = endpointConfiguration.UseTransport<AzureServiceBusTransport>();

            CustomizeEndpoint(transport, transportSettings);

            transport.ConfigureTransport(transportSettings, TransportTransactionMode.SendsAtomicWithReceive);
        }

        public override void CustomizeServiceControlEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = endpointConfiguration.UseTransport<AzureServiceBusTransport>();

            CustomizeEndpoint(transport, transportSettings);

            transport.ConfigureTransport(transportSettings, TransportTransactionMode.SendsAtomicWithReceive);
        }

        public override void CustomizeRawSendOnlyEndpoint(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = endpointConfiguration.UseTransport<AzureServiceBusTransport>();

            CustomizeEndpoint(transport, transportSettings);

            transport.ConfigureTransport(transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        public override void CustomizeSendOnlyEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = endpointConfiguration.UseTransport<AzureServiceBusTransport>();

            CustomizeEndpoint(transport, transportSettings);

            transport.ConfigureTransport(transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        public override void CustomizeForErrorIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = endpointConfiguration.UseTransport<AzureServiceBusTransport>();

            CustomizeEndpoint(transport, transportSettings);

            transport.ConfigureTransport(transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        public override IProvideQueueLength CreateQueueLengthProvider()
        {
            return new QueueLengthProvider();
        }

        void CustomizeEndpoint(TransportExtensions<AzureServiceBusTransport> transport, TransportSettings transportSettings)
        {
            var connectionString = transportSettings.ConnectionString;

            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

            if (builder.TryGetValue(TopicNamePart, out var topicName))
            {
                transport.TopicName((string)topicName);
            }

            if (builder.TryGetValue(TransportTypePart, out var transportTypeString) && Enum.TryParse((string)transportTypeString, true, out ServiceBusTransportType transportType) && transportType == ServiceBusTransportType.AmqpWebSockets)
            {
                transport.UseWebSockets();
            }
        }

        static string TopicNamePart = "TopicName";
        static string TransportTypePart = "TransportType";
    }
}