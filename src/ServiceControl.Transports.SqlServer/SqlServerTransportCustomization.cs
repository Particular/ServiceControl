namespace ServiceControl.Transports.SqlServer
{
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Raw;
    using ServiceBus.Management.Infrastructure.Settings;

    public class SqlServerTransportCustomization : TransportCustomization
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(SqlServerTransportCustomization));
        
        public override void CustomizeEndpoint(EndpointConfiguration endpointConfig, string connectionString)
        {          
            var transport = endpointConfig.UseTransport<SqlServerTransport>();
            transport.ConnectionString(connectionString);

            if (SettingsReader<bool>.Read("EnableDtc"))
            {
                transport.Transactions(TransportTransactionMode.TransactionScope);
                Logger.Info("DTC has been ENABLED");
            }
            else
            {
                transport.Transactions(TransportTransactionMode.ReceiveOnly);
                Logger.Info("DTC is DISABLED");
            }
            CustomizeEndpointTransport(transport);
        }

        public override void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, string connectionString)
        {
            var transport = endpointConfig.UseTransport<SqlServerTransport>();
            transport.ConnectionString(connectionString);
            transport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
            CustomizeRawEndpointTransport(transport);
        }

        protected virtual void CustomizeEndpointTransport(TransportExtensions<SqlServerTransport> extensions)
        {
        }

        protected virtual void CustomizeRawEndpointTransport(TransportExtensions<SqlServerTransport> extensions)
        {
        }
    }
}
