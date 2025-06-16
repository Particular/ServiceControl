namespace ServiceControl.Transports.PostgreSql;

using System.Linq;
using System.Runtime.CompilerServices;
using BrokerThroughput;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NServiceBus;
using NServiceBus.Transport.PostgreSql;
using ServiceControl.Infrastructure;

public class PostgreSqlTransportCustomization() : TransportCustomization<PostgreSqlTransport>
{
    protected override void CustomizeTransportForPrimaryEndpoint(EndpointConfiguration endpointConfiguration, PostgreSqlTransport transportDefinition, TransportSettings transportSettings)
    {
        transportDefinition.TransportTransactionMode = TransportTransactionMode.SendsAtomicWithReceive;

        transportSettings.MaxConcurrency ??= 10;
    }

    protected override void CustomizeTransportForAuditEndpoint(EndpointConfiguration endpointConfiguration, PostgreSqlTransport transportDefinition, TransportSettings transportSettings)
    {
        transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

        transportSettings.MaxConcurrency ??= 10;
    }

    protected override void CustomizeTransportForMonitoringEndpoint(EndpointConfiguration endpointConfiguration, PostgreSqlTransport transportDefinition, TransportSettings transportSettings)
    {
        transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

        transportSettings.MaxConcurrency ??= 10;
    }

    protected override void AddTransportForPrimaryCore(IServiceCollection services,
        TransportSettings transportSettings) =>
        services.AddSingleton<IBrokerThroughputQuery, PostgreSqlQuery>();

    protected override void AddTransportForMonitoringCore(IServiceCollection services, TransportSettings transportSettings)
    {
        services.AddSingleton<IProvideQueueLength, QueueLengthProvider>();
        services.AddHostedService(provider => provider.GetRequiredService<IProvideQueueLength>());
    }

    protected override PostgreSqlTransport CreateTransport(TransportSettings transportSettings, TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly)
    {
        var connectionString = transportSettings.ConnectionString.RemoveCustomConnectionStringParts(out var customSchema, out var subscriptionsTableSetting);

        var transport = new PostgreSqlTransport(connectionString);

        var subscriptions = transport.Subscriptions;

        if (customSchema != null)
        {
            transport.DefaultSchema = customSchema;
            subscriptions.SubscriptionTableName = new SubscriptionTableName(DefaultSubscriptionTableName, customSchema);
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
            logger.LogError("The EnableDtc setting is no longer supported natively within ServiceControl. If you require distributed transactions, you will have to use a Transport Adapter (https://docs.particular.net/servicecontrol/transport-adapter/)");
        }

        DisableDelayedDelivery(transport) = true;

        transport.TransportTransactionMode = transport.GetSupportedTransactionModes().Contains(preferredTransactionMode) ? preferredTransactionMode : TransportTransactionMode.ReceiveOnly;

        return transport;
    }

    protected override string ToTransportQualifiedQueueNameCore(string queueName)
    {
        const string delimiter = "\"";
        const string escapedDelimiter = delimiter + delimiter;

        if (queueName.StartsWith(delimiter) || queueName.EndsWith(delimiter))
        {
            return queueName;
        }

        return delimiter + queueName.Replace(delimiter, escapedDelimiter) + delimiter;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<DisableDelayedDelivery>k__BackingField")]
    static extern ref bool DisableDelayedDelivery(PostgreSqlTransport transport);

    const string DefaultSubscriptionTableName = "SubscriptionRouting";

    static readonly ILogger<PostgreSqlTransportCustomization> logger = LoggerUtil.CreateStaticLogger<PostgreSqlTransportCustomization>();
}