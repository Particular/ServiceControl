namespace ServiceBus.Management.AcceptanceTests.Contexts.TransportIntegration
{
    using System;
    using NServiceBus;

    public class AzureServiceBusTransportIntegration
    {
        public AzureServiceBusTransportIntegration()
        {
            ConnectionString = String.Empty; // empty on purpose
        }

        public string Name => "AzureServiceBus";

        public Type Type => typeof(AzureServiceBusTransport);

        public string TypeName => "NServiceBus.AzureServiceBusTransport, NServiceBus.Azure.Transports.WindowsAzureServiceBus";

        public string ConnectionString { get; set; }

        public void OnEndpointShutdown(string endpointName)
        {
        }

        public void TearDown()
        {

        }

        public void Setup()
        {

        }

        // TODO: This
        //class MyConfig : IProvideConfiguration<AzureServiceBusQueueConfig>
        //{
        //    public AzureServiceBusQueueConfig GetConfiguration()
        //    {
        //        return new AzureServiceBusQueueConfig
        //        {
        //            DefaultMessageTimeToLive = (long)TimeSpan.FromMinutes(15).TotalMilliseconds,
        //            LockDuration = (int)TimeSpan.FromSeconds(30).TotalMilliseconds,
        //            ServerWaitTime = 2,
        //            BatchSize = 5,
        //            MaxDeliveryCount = 100
        //        };
        //    }
        //}
    }
}