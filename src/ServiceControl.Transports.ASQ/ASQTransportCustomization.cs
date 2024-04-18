namespace ServiceControl.Transports.ASQ
{
    using System;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;

    public class ASQTransportCustomization : TransportCustomization<AzureStorageQueueTransport>
    {
        protected override void CustomizeTransportForPrimaryEndpoint(EndpointConfiguration endpointConfiguration, AzureStorageQueueTransport transportDefinition, TransportSettings transportSettings)
        {
            var routing = new RoutingSettings(endpointConfiguration.GetSettings());
            routing.EnableMessageDrivenPubSubCompatibilityMode();
        }

        //Do not ConfigurePubSub for send-only endpoint
        protected override void CustomizeTransportForAuditEndpoint(EndpointConfiguration endpointConfiguration, AzureStorageQueueTransport transportDefinition, TransportSettings transportSettings) { }

        protected override void CustomizeTransportForMonitoringEndpoint(EndpointConfiguration endpointConfiguration, AzureStorageQueueTransport transportDefinition, TransportSettings transportSettings) { }

        public override IProvideQueueLength CreateQueueLengthProvider() => new QueueLengthProvider();

        protected override AzureStorageQueueTransport CreateTransport(TransportSettings transportSettings, TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly)
        {
            var connectionString = transportSettings.ConnectionString.RemoveCustomConnectionStringParts(out var subscriptionTableName);

            var transport = new AzureStorageQueueTransport(connectionString, useNativeDelayedDeliveries: false)
            {
                QueueNameSanitizer = BackwardsCompatibleQueueNameSanitizer.Sanitize,
                MessageInvisibleTime = TimeSpan.FromMinutes(1)
            };

            if (!string.IsNullOrEmpty(subscriptionTableName))
            {
                transport.Subscriptions.SubscriptionTableName = subscriptionTableName;
            }

            transport.MessageWrapperSerializationDefinition = new SystemJsonSerializer();

            transport.TransportTransactionMode = transport.GetSupportedTransactionModes().Contains(preferredTransactionMode) ? preferredTransactionMode : TransportTransactionMode.ReceiveOnly;

            return transport;
        }
    }
}