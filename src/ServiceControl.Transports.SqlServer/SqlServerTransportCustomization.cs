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

            if (transportSettings.Get<bool>("TransportSettings.EnableDtc"))
            {
                Logger.Error("The EnableDtc setting is no longer supported natively within ServiceControl. If you require distributed transactions, you will have to use a Transport Adapter (https://docs.particular.net/servicecontrol/transport-adapter/)");
            }

            transport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
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