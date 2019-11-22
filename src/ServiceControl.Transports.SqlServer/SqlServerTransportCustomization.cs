namespace ServiceControl.Transports.SqlServer
{
    using System.Data.Common;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Logging;
    using NServiceBus.Raw;
    using NServiceBus.Transport.SQLServer;

    public class SqlServerTransportCustomization : TransportCustomization
    {
        public override void CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<SqlServerTransport>();
            ConfigureConnection(transport, transportSettings);

            if (transportSettings.GetOrDefault<bool>("TransportSettings.EnableDtc"))
            {
                Logger.Error("The EnableDtc setting is no longer supported natively within ServiceControl. If you require distributed transactions, you will have to use a Transport Adapter (https://docs.particular.net/servicecontrol/transport-adapter/)");
            }

            endpointConfig.GetSettings().Set("SqlServer.DisableDelayedDelivery", true);
            transport.NativeDelayedDelivery().DisableTimeoutManagerCompatibility();
            transport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
        }

        public override void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<SqlServerTransport>();
            ConfigureConnection(transport, transportSettings);
            endpointConfig.Settings.Set("SqlServer.DisableDelayedDelivery", true);

            transport.NativeDelayedDelivery().DisableTimeoutManagerCompatibility();
            transport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
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
                    schemaName:subscriptionsAddress.Schema ?? customSchema,
                    catalogName:subscriptionsAddress.Catalog
                );
            }

            transport.ConnectionString(connectionString);

            transport.EnableMessageDrivenPubSubCompatibilityMode();
        }

        public override IProvideQueueLength CreateQueueLengthProvider()
        {
            return new QueueLengthProvider();
        }

        const string defaultSubscriptionTableName = "SubscriptionRouting";
        static readonly ILog Logger = LogManager.GetLogger(typeof(SqlServerTransportCustomization));
    }
}