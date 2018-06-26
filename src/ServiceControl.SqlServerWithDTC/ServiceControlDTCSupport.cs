namespace ServiceControl.Transports.SqlServerWithDTC
{
    using NServiceBus;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Settings;

    // ReSharper disable once UnusedMember.Global
    public class ServiceControlDTCSupport : INeedInitialization
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(ServiceControlDTCSupport));
        public void Customize(EndpointConfiguration configuration)
        {
            if (SettingsReader<bool>.Read("EnableDtc"))
            {
                // TODO: Figure out how to add this back in
                //configuration.Transactions()
                //    .EnableDistributedTransactions()
                //    .WrapHandlersExecutionInATransactionScope();

                Logger.Info("DTC has been ENABLED");
                return;
            }

            Logger.Info("DTC is DISABLED");
        }
    }
}
