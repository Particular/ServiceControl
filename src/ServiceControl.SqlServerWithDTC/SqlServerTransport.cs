namespace ServiceControl.Transports.SqlServerWithDTC
{
    using NServiceBus;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Transport;

    public class SqlServerTransport : SqlServerTransportCustomization
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(SqlServerTransport));
        
        protected override void CustomizeTransport(EndpointConfiguration configuration, string connectionString)
        {
            var transport = configuration.UseTransport<NServiceBus.SqlServerTransport>();
            transport.ConnectionString(connectionString);
            
            if (SettingsReader<bool>.Read("EnableDtc"))
            {
                transport.Transactions(TransportTransactionMode.TransactionScope);

                Logger.Info("DTC has been ENABLED");
                return;
            }

            transport.Transactions(TransportTransactionMode.ReceiveOnly);
            Logger.Info("DTC is DISABLED");
        }
    }
}
