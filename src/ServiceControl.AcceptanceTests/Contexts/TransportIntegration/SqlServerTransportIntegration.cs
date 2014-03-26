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

        public string Name { get { return "SqlServer"; } }
        public Type Type { get { return typeof(SqlServer); } }
        public string ConnectionString { get; set; }

        public void SetUp()
        {
        }

        public void TearDown()
        {
        }
    }
}
