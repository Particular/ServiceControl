namespace ServiceControl.Transports.SqlServer
{
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Logging;
    using NServiceBus.Raw;
    using NServiceBus.Transport.SqlServer;

    public class SqlServerTransportCustomization : TransportCustomization<SqlServerTransport>
    {
        protected override void CustomizeTransportSpecificSendOnlyEndpointSettings(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeEndpoint(endpointConfiguration, transportSettings, TransportTransactionMode.ReceiveOnly);
            //Do not EnableMessageDrivenPubSubCompatibilityMode for send-only endpoint
        }

        protected override void CustomizeTransportSpecificServiceControlEndpointSettings(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = CustomizeEndpoint(endpointConfiguration, transportSettings, TransportTransactionMode.SendsAtomicWithReceive);
            transport.EnableMessageDrivenPubSubCompatibilityMode();
        }

        protected override void CustomizeRawSendOnlyEndpoint(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        protected override void CustomizeForQueueIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        protected override void CustomizeTransportSpecificMonitoringEndpointSettings(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeEndpoint(endpointConfiguration, transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        public override void CustomizeForReturnToSenderIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings, TransportTransactionMode.SendsAtomicWithReceive);
        }

        public override IProvideQueueLength CreateQueueLengthProvider()
        {
            return new QueueLengthProvider();
        }

        protected override SqlServerTransport CreateTransport(TransportSettings transportSettings)
        {
            var connectionString = transportSettings.ConnectionString
                .RemoveCustomConnectionStringParts(out var customSchema, out var subscriptionsTableSetting);

            var transport = new SqlServerTransport(connectionString);

            var subscriptions = transport.Subscriptions;

            if (customSchema != null)
            {
                transport.DefaultSchema = customSchema;
                subscriptions.SubscriptionTableName =
                    new SubscriptionTableName(defaultSubscriptionTableName, customSchema);
            }

            if (subscriptionsTableSetting != null)
            {
                var subscriptionsAddress = QueueAddress.Parse(subscriptionsTableSetting);

                subscriptions.SubscriptionTableName =
                    new SubscriptionTableName(subscriptionsAddress.Table,
                        subscriptionsAddress.Schema ?? customSchema,
                        subscriptionsAddress.Catalog);
            }

            return transport;
        }

        static void CustomizeEndpoint(EndpointConfiguration endpointConfig, SqlServerTransport transport, TransportSettings transportSettings, TransportTransactionMode transportTransactionMode)
        {
            if (transportSettings.GetOrDefault<bool>("TransportSettings.EnableDtc"))
            {
                Logger.Error("The EnableDtc setting is no longer supported natively within ServiceControl. If you require distributed transactions, you will have to use a Transport Adapter (https://docs.particular.net/servicecontrol/transport-adapter/)");
            }

            // TODO NSB8 we need to set this with reflection
            endpointConfig.GetSettings().Set("SqlServer.DisableDelayedDelivery", true);
            var sendOnlyEndpoint = endpointConfig.GetSettings().GetOrDefault<bool>("Endpoint.SendOnly");
            if (!sendOnlyEndpoint)
            {
                // TODO NSB8 How?
                transport.NativeDelayedDelivery();
            }

            transport.TransportTransactionMode = transportTransactionMode;
        }

        static void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, SqlServerTransport transport, TransportSettings transportSettings, TransportTransactionMode transportTransactionMode)
        {
            // TODO NSB8 Why should this be activiated on a raw endpoint???
            transport.EnableMessageDrivenPubSubCompatibilityMode();
            // TODO NSB8 we need to set this with reflection
            // endpointConfig.Settings.Set("SqlServer.DisableDelayedDelivery", true);

            var sendOnlyEndpoint = transport.GetSettings().GetOrDefault<bool>("Endpoint.SendOnly");
            if (!sendOnlyEndpoint)
            {
                transport.NativeDelayedDelivery();
            }

            transport.Transactions(transportTransactionMode);
        }

        const string defaultSubscriptionTableName = "SubscriptionRouting";

        static readonly ILog Logger = LogManager.GetLogger(typeof(SqlServerTransportCustomization));
    }
}