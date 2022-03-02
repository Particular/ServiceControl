namespace ServiceControl.Transports.ASBS
{
    using Azure.Identity;
    using NServiceBus;
    using NServiceBus.Raw;

    /*
    Sean has told a customer:

    Sorry to bug you. There's another option that should be supported by the Azure Service Bus client (not documented anywhere), a connection string that would enforce the client to use the Managed Identity authentication.
This would mean installing SC and replacing the connection string with an updated value that follows this format:
Endpoint=sb://<service-bus-resource>.servicebus.windows.net;Authentication=Managed Identity;

    It is documented here tho: https://docs.microsoft.com/en-us/dotnet/api/microsoft.servicebus.servicebusconnectionstringbuilder?view=azure-dotnet

     A TF wrote:

    Managed Identity support for Service Control
The new ASB transport supports MI via connection string OOTB and does not require anything special.
Legacy ASB transport does not support MI via connection string and requires transport customization (SC end).
Both ASB transports cannot use MI on-premises


    See remarks about the new sdk ignoring values https://docs.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.servicebusclient.-ctor?view=azure-dotnet#azure-messaging-servicebus-servicebusclient-ctor(system-string)
     */

    public class ASBSTransportCustomization : TransportCustomization
    {
        public override void CustomizeForAuditIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = endpointConfiguration.UseTransport<AzureServiceBusTransport>();

            //

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
            var connectionSettings = new ConnectionStringParser()
                .Parse(transportSettings.ConnectionString);

            if (connectionSettings.UseManagedIdentity)
            {
                if (connectionSettings.ClientId != null)
                {
                    transport.CustomTokenCredential(new ManagedIdentityCredential(connectionSettings.ClientId));
                }
                else
                {
                    transport.CustomTokenCredential(new ManagedIdentityCredential());
                }

                transport.ConnectionString(connectionSettings.FullyQualifiedNamespace);
            }
            else
            {
                transport.ConnectionString(connectionSettings.ConnectionString);
            }

            if (connectionSettings.TopicName != null)
            {
                transport.TopicName(connectionSettings.TopicName);
            }

            if (connectionSettings.UseWebSockets)
            {
                transport.UseWebSockets();
            }
        }
    }
}