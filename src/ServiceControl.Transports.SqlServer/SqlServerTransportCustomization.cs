﻿namespace ServiceControl.Transports.SqlServer
{
    using System.Linq;
    using System.Reflection;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Logging;
    using NServiceBus.Transport.SqlServer;

    public class SqlServerTransportCustomization : TransportCustomization<SqlServerTransport>
    {
        protected override void CustomizeTransportForPrimaryEndpoint(EndpointConfiguration endpointConfiguration, SqlServerTransport transportDefinition, TransportSettings transportSettings)
        {
            transportDefinition.TransportTransactionMode = TransportTransactionMode.SendsAtomicWithReceive;
            var routing = new RoutingSettings(endpointConfiguration.GetSettings());
            routing.EnableMessageDrivenPubSubCompatibilityMode();
        }

        //Do not EnableMessageDrivenPubSubCompatibilityMode for send-only endpoint
        protected override void CustomizeTransportForAuditEndpoint(EndpointConfiguration endpointConfiguration, SqlServerTransport transportDefinition, TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

        protected override void CustomizeTransportForMonitoringEndpoint(EndpointConfiguration endpointConfiguration, SqlServerTransport transportDefinition, TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

        public override IProvideQueueLength CreateQueueLengthProvider() => new QueueLengthProvider();

        protected override SqlServerTransport CreateTransport(TransportSettings transportSettings, TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly)
        {
            var connectionString = transportSettings.ConnectionString.RemoveCustomConnectionStringParts(out var customSchema, out var subscriptionsTableSetting);

            var transport = new SqlServerTransport(connectionString);

            var subscriptions = transport.Subscriptions;

            if (customSchema != null)
            {
                transport.DefaultSchema = customSchema;
                subscriptions.SubscriptionTableName = new SubscriptionTableName(defaultSubscriptionTableName, customSchema);
            }

            if (subscriptionsTableSetting != null)
            {
                var subscriptionsAddress = QueueAddress.Parse(subscriptionsTableSetting);

                subscriptions.SubscriptionTableName =
                    new SubscriptionTableName(subscriptionsAddress.Table,
                        subscriptionsAddress.Schema ?? customSchema,
                        subscriptionsAddress.Catalog);
            }

            if (transportSettings.GetOrDefault<bool>("TransportSettings.EnableDtc"))
            {
                Logger.Error("The EnableDtc setting is no longer supported natively within ServiceControl. If you require distributed transactions, you will have to use a Transport Adapter (https://docs.particular.net/servicecontrol/transport-adapter/)");
            }

            // TODO NSB8 replace with UnsafeAccessor?
            transport.GetType().GetProperty("DisableDelayedDelivery", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(transport, true);

            transport.TransportTransactionMode = transport.GetSupportedTransactionModes().Contains(preferredTransactionMode) ? preferredTransactionMode : TransportTransactionMode.ReceiveOnly;

            return transport;
        }

        const string defaultSubscriptionTableName = "SubscriptionRouting";

        static readonly ILog Logger = LogManager.GetLogger(typeof(SqlServerTransportCustomization));
    }
}