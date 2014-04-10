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

        public string Name { get { return "Msmq"; } }
        public Type Type { get { return typeof(Msmq); } }
        public string TypeName { get { return "NServiceBus.Core, NServiceBus.Transports.Msmq"; }}
        public string ConnectionString { get; set; }

        public void SetUp()
        {
        }

        public void TearDown()
        {
        }
    }
}
