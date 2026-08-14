namespace ServiceControl.AcceptanceTesting.InfrastructureConfig
{
    using System.Threading.Tasks;
    using EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;

#pragma warning disable PS0018
    public class ConfigureEndpointInMemoryPersistence : IConfigureEndpointTestExecution
    {
        public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
        {
            configuration.UsePersistence<NonDurablePersistence>();
            return Task.CompletedTask;
        }

        public Task Cleanup() =>
            // Nothing required for in-memory persistence
            Task.CompletedTask;
    }
#pragma warning restore PS0018
}