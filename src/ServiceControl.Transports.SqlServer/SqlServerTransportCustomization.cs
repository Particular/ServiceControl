namespace ServiceControl.Transports.SqlServer
{
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Raw;

    public class SqlServerTransportCustomization : TransportCustomization
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(SqlServerTransportCustomization));
        
        public override void CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {          
            var transport = endpointConfig.UseTransport<SqlServerTransport>();
            transport.ConnectionString(transportSettings.ConnectionString);

            if (transportSettings.Get<bool>("TransportSettings.EnableDtc"))
            {
                transport.Transactions(TransportTransactionMode.TransactionScope);
                Logger.Info("DTC has been ENABLED");
            }
            else
            {
                transport.Transactions(TransportTransactionMode.ReceiveOnly);
                Logger.Info("DTC is DISABLED");
            }
        }

        public override void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<SqlServerTransport>();
            transport.ConnectionString(transportSettings.ConnectionString);
            transport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
        }
    }
}
