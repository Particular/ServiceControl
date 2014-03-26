namespace ServiceBus.Management.AcceptanceTests.Contexts.TransportIntegration
{
    using System;

    public interface ITransportIntegration
    {
        string Name { get; }
        Type Type  { get; }
        string ConnectionString { get; set; }

        void SetUp();
        void TearDown();
    }
}
