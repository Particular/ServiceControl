namespace ServiceControl.AcceptanceTesting
{
    using EndpointTemplates;
    using NServiceBus.AcceptanceTesting.Support;

    public interface ITransportIntegration : IConfigureEndpointTestExecution
    {
        string Name { get; }
        string TypeName { get; }
        string ConnectionString { get; set; }
    }
}