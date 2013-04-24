namespace ServiceBus.Management
{
    using NServiceBus;

    /*
        This class configures this endpoint as a Server. More information about how to configure the NServiceBus host
        can be found here: http://nservicebus.com/GenericHost.aspx
    */
    public class EndpointConfig : IConfigureThisEndpoint, AsA_Server, IWantCustomLogging
    {
        public void Init()
        {
            SetLoggingLibrary.Log4Net(() => log4net.Config.XmlConfigurator.Configure());
        }
    }

    class TransactionSetup : INeedInitialization
    {
        public void Init()
        {
            Configure.Transactions.Advanced(t => t.DisableDistributedTransactions());
        }
    }
}