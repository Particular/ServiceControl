namespace ServiceControl.Transports.ASBS
{
    using System;
    using System.Data.Common;
    using Azure.Identity;
    using Azure.Messaging.ServiceBus;
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
            var connectionString = transportSettings.ConnectionString;

            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

            var shouldUseManagedIdentity = builder.TryGetValue("Authentication", out var authType) && (string)authType == "Managed Identity";

            if (shouldUseManagedIdentity)
            {
                if (builder.TryGetValue("ClientId", out var clientId))
                {
                    transport.CustomTokenCredential(new ManagedIdentityCredential((string)clientId));
                }
                else
                {
                    transport.CustomTokenCredential(new ManagedIdentityCredential());
                }

                var hasEndpoint = builder.TryGetValue("Endpoint", out var endpoint);
                if (!hasEndpoint)
                {
                    throw new Exception("Endpoint property is mandatory on the connection string");
                }

                var fullyQualifiedNamespace = endpoint.ToString().TrimEnd('/').Replace("sb://", "");
                transport.ConnectionString(fullyQualifiedNamespace);
            }
            else
            {
                transport.ConnectionString(transportSettings.ConnectionString);
            }
            //TODO check for SharedAccessKeyName and SharedAccessKey and if not present, use DefaultAzureCredentials 

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