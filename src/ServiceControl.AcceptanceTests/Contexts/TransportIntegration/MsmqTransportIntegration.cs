namespace ServiceBus.Management.AcceptanceTests.Contexts.TransportIntegration
{
    using System;
    using NServiceBus;

    public class MsmqTransportIntegration : ITransportIntegration
    {
        public MsmqTransportIntegration()
        {
            ConnectionString = @"cacheSendConnection=false;journal=false;"; // Default connstr
        }

        public string Name => "Msmq";
        public Type Type => typeof(MsmqTransport);
        public string TypeName => "NServiceBus.MsmqTransport, NServiceBus.Core";
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
    }
}
