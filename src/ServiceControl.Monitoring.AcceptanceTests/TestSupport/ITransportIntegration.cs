namespace ServiceBus.Management.AcceptanceTests
{
    using NServiceBus.AcceptanceTesting.Support;

    public interface ITransportIntegration : IConfigureEndpointTestExecution
    {
        string TypeName { get; }
        string ConnectionString { get; set; }
    }
}