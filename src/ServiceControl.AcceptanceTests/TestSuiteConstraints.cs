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
            var transportCustomizationToUseString = Environment.GetEnvironmentVariable("ServiceControl.AcceptanceTests.TransportCustomization") ?? typeof(ConfigureEndpointMsmqTransport).Name;
            var transportToUse = (ITransportIntegration) Activator.CreateInstance(Type.GetType(transportCustomizationToUseString));

            var connectionString = Environment.GetEnvironmentVariable("ServiceControl.AcceptanceTests.ConnectionString");
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                transportToUse.ConnectionString = connectionString; 
            }

            return transportToUse;
        }
    }
}