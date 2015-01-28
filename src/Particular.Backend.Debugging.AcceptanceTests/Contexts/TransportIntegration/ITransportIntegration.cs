namespace Particular.Backend.Debugging.AcceptanceTests.Contexts.TransportIntegration
{
    using System;

    public interface ITransportIntegration
    {
        string Name { get; }
        Type Type  { get; }
        string TypeName { get; }
        string ConnectionString { get; set; }

        void SetUp();
        void TearDown();
    }
}
