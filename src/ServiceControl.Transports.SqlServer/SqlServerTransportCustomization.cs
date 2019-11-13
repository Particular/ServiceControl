namespace ServiceControl.Transports.SqlServer
{
    using System.Data.Common;
    using NServiceBus;
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

            transport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
        }

        public override void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<SqlServerTransport>();
            ConfigureConnection(transport, transportSettings);
            transport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
        }

        static void ConfigureConnection(TransportExtensions<SqlServerTransport> transport, TransportSettings transportSettings)
        {
            var builder = new DbConnectionStringBuilder
            {
                ConnectionString = transportSettings.ConnectionString
            };

            if (builder.TryGetValue(queueSchemaName, out var customSchema))
            {
                builder.Remove(queueSchemaName);

                transport.DefaultSchema((string)customSchema);
            }

            transport.ConnectionString(builder.ConnectionString);
        }

        public override IProvideQueueLengthNew CreateQueueLengthProvider()
        {
            return new QueueLengthProvider();
        }

        const string queueSchemaName = "Queue Schema";
        static readonly ILog Logger = LogManager.GetLogger(typeof(SqlServerTransportCustomization));
    }
}