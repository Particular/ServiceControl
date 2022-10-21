namespace ServiceControl.AcceptanceTesting.InfrastructureConfig
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using ServiceControlInstaller.Engine.Instances;
    using Transports.ASQ;

    public class ConfigureEndpointAzureStorageQueueTransport : ITransportIntegration
    {
        public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
        {
            var transportConfig = configuration
                .UseTransport<AzureStorageQueueTransport>()
                .ConnectionString(ConnectionString)
                .MessageInvisibleTime(TimeSpan.FromSeconds(30));

            transportConfig.SanitizeQueueNamesWith(BackwardsCompatibleQueueNameSanitizer.Sanitize);

            transportConfig.DelayedDelivery().DisableTimeoutManager();

            return Task.FromResult(0);
        }

        public Task Cleanup()
        {
            return Task.FromResult(0);
        }

        public string Name => TransportNames.AzureStorageQueue;
        public string TypeName => $"{typeof(ASQTransportCustomization).AssemblyQualifiedName}";
        public string ConnectionString { get; set; }
    }
}