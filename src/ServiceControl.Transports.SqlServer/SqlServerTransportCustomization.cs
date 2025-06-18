namespace ServiceControl.Transports.SqlServer
{
    using System.Linq;
    using System.Runtime.CompilerServices;
    using BrokerThroughput;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Transport.SqlServer;
    using ServiceControl.Infrastructure;

    public class SqlServerTransportCustomization() : TransportCustomization<SqlServerTransport>
    {
        protected override void CustomizeTransportForPrimaryEndpoint(EndpointConfiguration endpointConfiguration, SqlServerTransport transportDefinition, TransportSettings transportSettings)
        {
            transportDefinition.TransportTransactionMode = TransportTransactionMode.SendsAtomicWithReceive;
            var routing = new RoutingSettings(endpointConfiguration.GetSettings());
            routing.EnableMessageDrivenPubSubCompatibilityMode();

            transportSettings.MaxConcurrency ??= 10;
        }

        //Do not EnableMessageDrivenPubSubCompatibilityMode for send-only endpoint
        protected override void CustomizeTransportForAuditEndpoint(EndpointConfiguration endpointConfiguration, SqlServerTransport transportDefinition, TransportSettings transportSettings)
        {
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

            transportSettings.MaxConcurrency ??= 10;
        }

        protected override void CustomizeTransportForMonitoringEndpoint(EndpointConfiguration endpointConfiguration, SqlServerTransport transportDefinition, TransportSettings transportSettings)
        {
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

            transportSettings.MaxConcurrency ??= 10;
        }

        protected override void AddTransportForPrimaryCore(IServiceCollection services,
            TransportSettings transportSettings)
        {
            services.AddSingleton<IBrokerThroughputQuery, SqlServerQuery>();
        }

        protected override void AddTransportForMonitoringCore(IServiceCollection services, TransportSettings transportSettings)
        {
            services.AddSingleton<IProvideQueueLength, QueueLengthProvider>();
            services.AddHostedService(provider => provider.GetRequiredService<IProvideQueueLength>());
        }

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
                logger.LogError("The EnableDtc setting is no longer supported natively within ServiceControl. If you require distributed transactions, you will have to use a Transport Adapter (https://docs.particular.net/servicecontrol/transport-adapter/)");
            }

            DisableDelayedDelivery(transport) = true;

            transport.TransportTransactionMode = transport.GetSupportedTransactionModes().Contains(preferredTransactionMode) ? preferredTransactionMode : TransportTransactionMode.ReceiveOnly;

            return transport;
        }

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<DisableDelayedDelivery>k__BackingField")]
        static extern ref bool DisableDelayedDelivery(SqlServerTransport transport);

        const string defaultSubscriptionTableName = "SubscriptionRouting";

        static readonly ILogger<SqlServerTransportCustomization> logger = LoggerUtil.CreateStaticLogger<SqlServerTransportCustomization>();
    }
}