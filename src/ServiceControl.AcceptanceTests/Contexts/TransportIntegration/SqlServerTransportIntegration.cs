namespace ServiceBus.Management.AcceptanceTests.Contexts.TransportIntegration
{
    using System;
    using NServiceBus;

    public class SqlServerTransportIntegration : ITransportIntegration
    {
        public SqlServerTransportIntegration()
        {
            ConnectionString = @"Server=localhost\sqlexpress;Database=nservicebus;Trusted_Connection=True;"; // Default connstr
        }

        public string Name => "SqlServer";
        public Type Type => typeof(SqlServerTransport);
        public string TypeName => "NServiceBus.SqlServerTransport, NServiceBus.Transports.SqlServer";
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
