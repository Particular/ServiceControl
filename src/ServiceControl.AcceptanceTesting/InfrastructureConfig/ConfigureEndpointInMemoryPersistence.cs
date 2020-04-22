namespace ServiceControl.AcceptanceTesting.InfrastructureConfig
{
    using System.Threading.Tasks;
    using EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;

    public class ConfigureEndpointInMemoryPersistence : IConfigureEndpointTestExecution
    {
        public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
        {
            configuration.UsePersistence<InMemoryPersistence>();
            return Task.FromResult(0);
        }

        public Task Cleanup()
        {
            // Nothing required for in-memory persistence
            return Task.FromResult(0);
        }
    }
}