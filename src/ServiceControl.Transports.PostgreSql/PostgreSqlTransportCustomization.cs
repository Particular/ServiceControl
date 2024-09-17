namespace ServiceControl.Transports.PostgreSql
{
    using System.Linq;
    using System.Runtime.CompilerServices;
    using BrokerThroughput;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Transport.PostgreSql;

    public class PostgreSqlTransportCustomization : TransportCustomization<PostgreSqlTransport>
    {
        protected override void CustomizeTransportForPrimaryEndpoint(EndpointConfiguration endpointConfiguration, PostgreSqlTransport transportDefinition, TransportSettings transportSettings)
        {
            transportDefinition.TransportTransactionMode = TransportTransactionMode.SendsAtomicWithReceive;
            //var routing = new RoutingSettings(endpointConfiguration.GetSettings());
            //routing.EnableMessageDrivenPubSubCompatibilityMode();
        }

        //Do not EnableMessageDrivenPubSubCompatibilityMode for send-only endpoint
        protected override void CustomizeTransportForAuditEndpoint(EndpointConfiguration endpointConfiguration, PostgreSqlTransport transportDefinition, TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

        protected override void CustomizeTransportForMonitoringEndpoint(EndpointConfiguration endpointConfiguration, PostgreSqlTransport transportDefinition, TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

        protected override void AddTransportForPrimaryCore(IServiceCollection services,
            TransportSettings transportSettings)
        {
            services.AddSingleton<IBrokerThroughputQuery, PostgreSqlQuery>();
            transportSettings.ErrorQueue = ToTransportQualifiedQueueName(transportSettings.ErrorQueue);
        }

        protected override void AddTransportForMonitoringCore(IServiceCollection services, TransportSettings transportSettings)
        {
            services.AddSingleton<IProvideQueueLength, QueueLengthProvider>();
            services.AddHostedService(provider => provider.GetRequiredService<IProvideQueueLength>());
            transportSettings.ErrorQueue = ToTransportQualifiedQueueName(transportSettings.ErrorQueue);
        }


        protected override PostgreSqlTransport CreateTransport(TransportSettings transportSettings, TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly)
        {
            var connectionString = transportSettings.ConnectionString.RemoveCustomConnectionStringParts(out var customSchema, out var subscriptionsTableSetting);

            var transport = new PostgreSqlTransport(connectionString);

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
                        subscriptionsAddress.Schema ?? customSchema);
            }

            if (transportSettings.GetOrDefault<bool>("TransportSettings.EnableDtc"))
            {
                Logger.Error("The EnableDtc setting is no longer supported natively within ServiceControl. If you require distributed transactions, you will have to use a Transport Adapter (https://docs.particular.net/servicecontrol/transport-adapter/)");
            }

            DisableDelayedDelivery(transport) = true;

            transport.TransportTransactionMode = transport.GetSupportedTransactionModes().Contains(preferredTransactionMode) ? preferredTransactionMode : TransportTransactionMode.ReceiveOnly;

            return transport;
        }

        protected override string ToTransportQualifiedQueueNameCore(string queueName)
        {
            string Delimiter = "\"";
            string EscapedDelimiter = Delimiter + Delimiter;
            if (queueName.StartsWith(Delimiter) || queueName.EndsWith(Delimiter))
            {
                return queueName;
            }

            return Delimiter + queueName.Replace(Delimiter, EscapedDelimiter) + Delimiter;
        }

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<DisableDelayedDelivery>k__BackingField")]
        static extern ref bool DisableDelayedDelivery(PostgreSqlTransport transport);

        const string defaultSubscriptionTableName = "SubscriptionRouting";

        static readonly ILog Logger = LogManager.GetLogger(typeof(PostgreSqlTransportCustomization));
    }
}