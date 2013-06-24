namespace ServiceBus.Management
{
    using System.IO;
    using System.Reflection;
    using NServiceBus;

    /*
        This class configures this endpoint as a Server. More information about how to configure the NServiceBus host
        can be found here: http://nservicebus.com/GenericHost.aspx
    */
    public class EndpointConfig : IConfigureThisEndpoint, AsA_Server, IWantCustomLogging, IWantCustomInitialization
    {
        public void Init()
        {
            SetLoggingLibrary.Log4Net(() => log4net.Config.XmlConfigurator.Configure());

            using (var licenseStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ServiceBus.Management.License.xml"))
            using (var sr = new StreamReader(licenseStream))
            {
                Configure.Instance.License(sr.ReadToEnd());
            }

            Configure.Transactions.Advanced(t => t.DisableDistributedTransactions());
        }
    }
}