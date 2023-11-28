namespace ServiceControl.Transports.ASQ
{
    using NServiceBus;
    using System;
    using NServiceBus.Configuration.AdvancedExtensibility;

    public class ASQTransportCustomization : TransportCustomization<AzureStorageQueueTransport>
    {
        protected override void CustomizeForQueueIngestion(AzureStorageQueueTransport transportDefinition, TransportSettings transportSettings)
            => CustomizeRawEndpoint(transportDefinition);

        protected override void CustomizeTransportSpecificMonitoringEndpointSettings(
            EndpointConfiguration endpointConfiguration,
            AzureStorageQueueTransport transportDefinition,
            TransportSettings transportSettings)
        {
        }

        protected override void CustomizeForReturnToSenderIngestion(AzureStorageQueueTransport transportDefinition, TransportSettings transportSettings)
            => CustomizeRawEndpoint(transportDefinition);

        protected override void CustomizeTransportSpecificServiceControlEndpointSettings(
            EndpointConfiguration endpointConfiguration,
            AzureStorageQueueTransport transportDefinition,
            TransportSettings transportSettings)
        {
            var routing = new RoutingSettings(endpointConfiguration.GetSettings());
            routing.EnableMessageDrivenPubSubCompatibilityMode();
        }

        protected override void CustomizeRawSendOnlyEndpoint(AzureStorageQueueTransport transportDefinition, TransportSettings transportSettings)
            => CustomizeRawEndpoint(transportDefinition);

        protected override void CustomizeTransportSpecificSendOnlyEndpointSettings(
            EndpointConfiguration endpointConfiguration,
            AzureStorageQueueTransport transportDefinition,
            TransportSettings transportSettings)
        {
            //Do not ConfigurePubSub for send-only endpoint
            var endpointName = endpointConfiguration.GetSettings().EndpointName();
            transportDefinition.DelayedDelivery.DelayedDeliveryPoisonQueue = endpointName + ".poison";
        }

        protected override AzureStorageQueueTransport CreateTransport(TransportSettings transportSettings)
        {
            var connectionString = transportSettings.ConnectionString
                .RemoveCustomConnectionStringParts(out var subscriptionTableName);

            var transport = new AzureStorageQueueTransport(connectionString)
            {
                TransportTransactionMode = TransportTransactionMode.ReceiveOnly,
                QueueNameSanitizer = BackwardsCompatibleQueueNameSanitizer.Sanitize,
                MessageInvisibleTime = TimeSpan.FromMinutes(1)
            };

            if (!string.IsNullOrEmpty(subscriptionTableName))
            {
                transport.Subscriptions.SubscriptionTableName = subscriptionTableName;
            }

            return transport;
        }

        static void CustomizeRawEndpoint(AzureStorageQueueTransport transportDefinition)
            => transportDefinition.MessageWrapperSerializationDefinition = new NewtonsoftJsonSerializer();

        public override IProvideQueueLength CreateQueueLengthProvider() => new QueueLengthProvider();
    }
}