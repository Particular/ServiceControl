namespace ServiceControl.Transports.ASBS
{
    using Azure.Identity;
    using NServiceBus;
    using NServiceBus.Raw;

    public class ASBSTransportCustomization : TransportCustomization
    {
        public override void CustomizeForAuditIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = endpointConfiguration.UseTransport<AzureServiceBusTransport>();

            CustomizeEndpoint(transport, transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        public override void CustomizeForMonitoringIngestion(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = endpointConfiguration.UseTransport<AzureServiceBusTransport>();

            CustomizeEndpoint(transport, transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        public override void CustomizeForReturnToSenderIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = endpointConfiguration.UseTransport<AzureServiceBusTransport>();

            CustomizeEndpoint(transport, transportSettings, TransportTransactionMode.SendsAtomicWithReceive);
        }

        public override void CustomizeServiceControlEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = endpointConfiguration.UseTransport<AzureServiceBusTransport>();

            CustomizeEndpoint(transport, transportSettings, TransportTransactionMode.SendsAtomicWithReceive);
        }

        public override void CustomizeRawSendOnlyEndpoint(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = endpointConfiguration.UseTransport<AzureServiceBusTransport>();

            CustomizeEndpoint(transport, transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        public override void CustomizeSendOnlyEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = endpointConfiguration.UseTransport<AzureServiceBusTransport>();

            CustomizeEndpoint(transport, transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        public override void CustomizeForErrorIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = endpointConfiguration.UseTransport<AzureServiceBusTransport>();

            CustomizeEndpoint(transport, transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        public override IProvideQueueLength CreateQueueLengthProvider()
        {
            return new QueueLengthProvider();
        }

        void CustomizeEndpoint(
            TransportExtensions<AzureServiceBusTransport> transport,
            TransportSettings transportSettings,
            TransportTransactionMode transportTransactionMode)
        {
            var connectionSettings = ConnectionStringParser.Parse(transportSettings.ConnectionString);

            if (connectionSettings.UseDefaultCredentials)
            {
                transport.CustomTokenCredential(new DefaultAzureCredential());
            }
            else if (connectionSettings.UseManagedIdentity)
            {
                if (connectionSettings.ClientId != null)
                {
                    transport.CustomTokenCredential(new ManagedIdentityCredential(connectionSettings.ClientId));
                }
                else
                {
                    transport.CustomTokenCredential(new ManagedIdentityCredential());
                }
            }

            transport.ConnectionString(connectionSettings.TransportConnectionString);

            if (connectionSettings.TopicName != null)
            {
                transport.TopicName(connectionSettings.TopicName);
            }

            if (connectionSettings.UseWebSockets)
            {
                transport.UseWebSockets();
            }

            transport.ConfigureNameShorteners();
            transport.Transactions(transportTransactionMode);
        }
    }
}