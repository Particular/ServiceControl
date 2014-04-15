namespace ServiceBus.Management.AcceptanceTests.Contexts.TransportIntegration
{
    using System;
    using NServiceBus;

    public class AzureServiceBusTransportIntegration : ITransportIntegration
    {
        public AzureServiceBusTransportIntegration()
        {
            ConnectionString = ""; // empty on purpse
        }

        public string Name { get { return "AzureServiceBus"; } }
        public Type Type { get { return typeof(AzureServiceBus); } }
        public string TypeName { get { return "NServiceBus.AzureServiceBus, NServiceBus.Azure.Transports.WindowsAzureServiceBus"; } }
        public string ConnectionString { get; set; }

        public void SetUp()
        {

        }

        public void TearDown()
        {

        }
    }
}
