namespace ServiceControl.AcceptanceTesting.InfrastructureConfig
{
    using System.IO;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using NUnit.Framework;
    using Transports.Learning;

    public class ConfigureEndpointLearningTransport : ITransportIntegration
    {
        public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
        {
            Directory.CreateDirectory(ConnectionString);

            var transportConfig = configuration.UseTransport<LearningTransport>();
            transportConfig.StorageDirectory(ConnectionString);
            transportConfig.NoPayloadSizeRestriction();

            return Task.FromResult(0);
        }

        public Task Cleanup()
        {
            if (Directory.Exists(ConnectionString))
            {
                Directory.Delete(ConnectionString, true);
            }

            return Task.FromResult(0);
        }

        public string Name => "Learning";
        public string TypeName => $"{typeof(LearningTransportCustomization).AssemblyQualifiedName}";
        public string ConnectionString { get; set; } = Path.Combine(TestContext.CurrentContext.TestDirectory, @"..\..\..\.transport");
    }
}