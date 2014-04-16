using System;

namespace ServiceBus.Management.AcceptanceTests.Contexts.TransportIntegration
{
    public class ActiveMqTransportIntegration : ITransportIntegration
    {
        public ActiveMqTransportIntegration()
        {
            ConnectionString = @"ServerUrl=activemq:tcp://localhost:61616"; // Default connstr
        }

        public string Name { get { return "ActiveMq"; } }
        public Type Type { get { throw new NotSupportedException(); } }
        public string TypeName { get{ throw new NotSupportedException();} }
        public string ConnectionString { get; set; }
        public void SetUp()
        {

        }

        public void TearDown()
        {
        }
    }
}
