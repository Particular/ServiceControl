namespace ServiceControl.Transports.SqlServerWithDTC
{
    using NServiceBus;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Settings;

    // ReSharper disable once UnusedMember.Global
    public class ServiceControlDTCSupport : INeedInitialization
    {
        public void Customize(BusConfiguration configuration)
        {
            if (SettingsReader<bool>.Read("EnableDtc"))
            {
                configuration.Transactions()
                    .EnableDistributedTransactions()
                    .WrapHandlersExecutionInATransactionScope();

                Logger.Info("DTC has been ENABLED");
                return;
            }

            Logger.Info("DTC is DISABLED");
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(ServiceControlDTCSupport));
    }
}
