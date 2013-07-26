namespace ServiceBus.Management
{
    using System;
    using System.IO;
    using System.Reflection;
    using NServiceBus;

    public class EndpointConfig : IConfigureThisEndpoint, AsA_Server, IWantCustomLogging, IWantCustomInitialization
    {
        public void Init()
        {
            SetLoggingLibrary.Log4Net(() => log4net.Config.XmlConfigurator.Configure());

            var transportType = SettingsReader<string>.Read("TransportType", typeof(Msmq).AssemblyQualifiedName);
            Configure.With().UseTransport(Type.GetType(transportType));

            using (var licenseStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ServiceBus.Management.License.xml"))
            using (var sr = new StreamReader(licenseStream))
            {
                Configure.Instance.License(sr.ReadToEnd());
            }

            Configure.Transactions.Advanced(t => t.DisableDistributedTransactions());
        }
    }
}