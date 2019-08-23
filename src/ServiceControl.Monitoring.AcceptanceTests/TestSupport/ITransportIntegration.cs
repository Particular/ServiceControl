namespace ServiceBus.Management.AcceptanceTests
{
    using NServiceBus.AcceptanceTesting.Support;

    public interface ITransportIntegration : IConfigureEndpointTestExecution
    {
        string MonitoringSeamTypeName { get; }
        string ConnectionString { get; set; }
    }
}