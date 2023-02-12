namespace ServiceControl.Transports.ASQ
{
    using NServiceBus;
    using NServiceBus.Raw;
    using NServiceBus.Transport;
    using System;
    using System.Threading.Tasks;

    public class ASQTransportCustomization : TransportCustomization
    {

        public override void CustomizeForAuditIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings);
        }

        public override void CustomizeForMonitoringIngestion(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeEndpoint(endpointConfiguration, transportSettings);
        }

        public override void CustomizeForReturnToSenderIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings);
        }

        public override void CustomizeServiceControlEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = CustomizeEndpoint(endpointConfiguration, transportSettings);
            transport.EnableMessageDrivenPubSubCompatibilityMode();
        }

        public override void CustomizeRawSendOnlyEndpoint(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings);
        }

        public override void CustomizeSendOnlyEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeEndpoint(endpointConfiguration, transportSettings);
            //Do not ConfigurePubSub for send-only endpoint
        }

        public override void CustomizeForErrorIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings);
        }

        static TransportExtensions<AzureStorageQueueTransport> CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<AzureStorageQueueTransport>();
            ConfigureTransport(transport, transportSettings);
            return transport;
        }

        static void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<AzureStorageQueueTransport>();
            transport.ApplyHacksForNsbRaw();
            ConfigureTransport(transport, transportSettings);
        }

        static void ConfigureTransport(TransportExtensions<AzureStorageQueueTransport> transport, TransportSettings transportSettings)
        {
            var connectionString = transportSettings.ConnectionString
                .RemoveCustomConnectionStringParts(out var subscriptionTableName);

            transport.SanitizeQueueNamesWith(BackwardsCompatibleQueueNameSanitizer.Sanitize);
            transport.Transactions(TransportTransactionMode.ReceiveOnly);
            transport.ConnectionString(connectionString);

            if (!string.IsNullOrEmpty(subscriptionTableName))
            {
                transport.SubscriptionTableName(subscriptionTableName);
            }

            transport.MessageInvisibleTime(TimeSpan.FromMinutes(1));
        }

        public override IProvideQueueLength CreateQueueLengthProvider()
        {
            return new QueueLengthProvider();
        }

        public override Task<IQueueIngestor> InitializeIngestor(string queueName, TransportSettings transportSettings, Func<MessageContext, Task> onMessage, IErrorHandlingPolicy onError, Func<string, Exception, Task> onCriticalError) => throw new NotImplementedException();
    }
}