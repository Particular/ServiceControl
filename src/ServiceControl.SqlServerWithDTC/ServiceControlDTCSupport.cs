namespace ServiceControl.Transports.SqlServerWithDTC
{
    using NServiceBus;

    public class ServiceControlDTCSupport : INeedInitialization
    {
        public void Customize(BusConfiguration configuration)
        {
            configuration.Transactions()
                .EnableDistributedTransactions()
                .WrapHandlersExecutionInATransactionScope();
        }
    }
}
