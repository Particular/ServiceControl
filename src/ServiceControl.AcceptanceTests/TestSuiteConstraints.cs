namespace NServiceBus.AcceptanceTests
{
    using System;
    using AcceptanceTesting.Support;
    using ServiceBus.Management.AcceptanceTests;

    public partial class TestSuiteConstraints
    {
        public bool SupportsDtc => false;
        public bool SupportsCrossQueueTransactions => true;
        public bool SupportsNativePubSub => true;
        public bool SupportsNativeDeferral => true;
        public bool SupportsOutbox => false;
        public IConfigureEndpointTestExecution CreateTransportConfiguration() => GetTransportIntegrationFromEnvironmentVar();
        public IConfigureEndpointTestExecution CreatePersistenceConfiguration() => new ConfigureEndpointInMemoryPersistence();
        
        private static ITransportIntegration GetTransportIntegrationFromEnvironmentVar()
        {
            ITransportIntegration transportToUse = null;

            var transportToUseString = Environment.GetEnvironmentVariable("ServiceControl.AcceptanceTests.Transport");
            if (transportToUseString != null)
            {
                transportToUse = (ITransportIntegration) Activator.CreateInstance(Type.GetType(typeof(ConfigureEndpointMsmqTransport).FullName.Replace("Msmq", transportToUseString)) ?? typeof(ConfigureEndpointMsmqTransport));
            }

            if (transportToUse == null)
            {
                transportToUse = new ConfigureEndpointMsmqTransport();
            }

            var connectionString = Environment.GetEnvironmentVariable("ServiceControl.AcceptanceTests.ConnectionString");
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                transportToUse.ConnectionString = connectionString; 
            }

            return transportToUse;
        }
    }
}