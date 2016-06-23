namespace ServiceControl.Transports.SqlServerWithDTC
{
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Settings;

    public class ServiceControlDTCSupport : INeedInitialization
    {
        public void Customize(BusConfiguration configuration)
        {
            if (SettingsReader<bool>.Read("EnableDtc"))
            {
                configuration.Transactions()
                    .EnableDistributedTransactions()
                    .WrapHandlersExecutionInATransactionScope();
            }
        }
    }
}
