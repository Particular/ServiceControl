namespace ServiceControl.Transports.SqlServer
{
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Raw;

    public class SqlServerTransportCustomization : TransportCustomization
    {
        public override void CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<SqlServerTransport>();
            transport.ConnectionString(transportSettings.ConnectionString);
            transport.Transactions(TransportTransactionMode.ReceiveOnly);
        }

        public override void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<SqlServerTransport>();
            transport.ConnectionString(transportSettings.ConnectionString);
            transport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(SqlServerTransportCustomization));
    }
}