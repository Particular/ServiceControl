namespace ServiceControl.Transports.ASQ
{
    using System;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;

    public class ASQTransportCustomization : TransportCustomization<AzureStorageQueueTransport>
    {
        protected override void CustomizeTransportForMonitoringEndpoint(
            EndpointConfiguration endpointConfiguration,
            AzureStorageQueueTransport transportDefinition,
            TransportSettings transportSettings)
        {
        }

        protected override void CustomizeForReturnToSenderIngestion(AzureStorageQueueTransport transportDefinition, TransportSettings transportSettings) { }

        protected override void CustomizeTransportForPrimaryEndpoint(
            EndpointConfiguration endpointConfiguration,
            AzureStorageQueueTransport transportDefinition,
            TransportSettings transportSettings)
        {
            var routing = new RoutingSettings(endpointConfiguration.GetSettings());
            routing.EnableMessageDrivenPubSubCompatibilityMode();
        }

        protected override void CustomizeTransportForAuditEndpoint(
            EndpointConfiguration endpointConfiguration,
            AzureStorageQueueTransport transportDefinition,
            TransportSettings transportSettings)
        {
            //Do not ConfigurePubSub for send-only endpoint
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

            transport.MessageWrapperSerializationDefinition = new NewtonsoftJsonSerializer();
            transport.DelayedDelivery.DelayedDeliveryPoisonQueue = transportSettings.ErrorQueue;

            return transport;
        }

        public override IProvideQueueLength CreateQueueLengthProvider() => new QueueLengthProvider();
    }
}