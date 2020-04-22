namespace ServiceControl.Transports.SqlServer
{
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Logging;
    using NServiceBus.Raw;

    public class SqlServerTransportCustomization : TransportCustomization
    {
        public override void CustomizeSendOnlyEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeEndpoint(endpointConfiguration, transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        public override void CustomizeServiceControlEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeEndpoint(endpointConfiguration, transportSettings, TransportTransactionMode.SendsAtomicWithReceive);
        }

        public override void CustomizeRawSendOnlyEndpoint(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        public override void CustomizeForErrorIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        public override void CustomizeForAuditIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        public override void CustomizeForMonitoringIngestion(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
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

        static void CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportSettings transportSettings, TransportTransactionMode transportTransactionMode)
        {
            var transport = endpointConfig.UseTransport<SqlServerTransport>();
            ConfigureConnection(transport, transportSettings);

            if (transportSettings.GetOrDefault<bool>("TransportSettings.EnableDtc"))
            {
                Logger.Error("The EnableDtc setting is no longer supported natively within ServiceControl. If you require distributed transactions, you will have to use a Transport Adapter (https://docs.particular.net/servicecontrol/transport-adapter/)");
            }

            endpointConfig.GetSettings().Set("SqlServer.DisableDelayedDelivery", true);
            var sendOnlyEndpoint = transport.GetSettings().GetOrDefault<bool>("Endpoint.SendOnly");
            if (!sendOnlyEndpoint)
            {
                transport.NativeDelayedDelivery();
            }

            transport.Transactions(transportTransactionMode);
        }

        static void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings, TransportTransactionMode transportTransactionMode)
        {
            var transport = endpointConfig.UseTransport<SqlServerTransport>();
            ConfigureConnection(transport, transportSettings);
            endpointConfig.Settings.Set("SqlServer.DisableDelayedDelivery", true);

            var sendOnlyEndpoint = transport.GetSettings().GetOrDefault<bool>("Endpoint.SendOnly");
            if (!sendOnlyEndpoint)
            {
                transport.NativeDelayedDelivery();
            }

            transport.Transactions(transportTransactionMode);
        }

        static void ConfigureConnection(TransportExtensions<SqlServerTransport> transport, TransportSettings transportSettings)
        {
            var connectionString = transportSettings.ConnectionString
                .RemoveCustomConnectionStringParts(out var customSchema, out var subscriptionsTableSetting);

            var subscriptions = transport.SubscriptionSettings();

            if (customSchema != null)
            {
                transport.DefaultSchema(customSchema);
                subscriptions.SubscriptionTableName(defaultSubscriptionTableName, customSchema);
            }

            if (subscriptionsTableSetting != null)
            {
                var subscriptionsAddress = QueueAddress.Parse(subscriptionsTableSetting);

                subscriptions.SubscriptionTableName(
                    tableName: subscriptionsAddress.Table,
                    schemaName: subscriptionsAddress.Schema ?? customSchema,
                    catalogName: subscriptionsAddress.Catalog
                );
            }

            transport.ConnectionString(connectionString);

            transport.EnableMessageDrivenPubSubCompatibilityMode();
        }

        const string defaultSubscriptionTableName = "SubscriptionRouting";

        static readonly ILog Logger = LogManager.GetLogger(typeof(SqlServerTransportCustomization));
    }
}