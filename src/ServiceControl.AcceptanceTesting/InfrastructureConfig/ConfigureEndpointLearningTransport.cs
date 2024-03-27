namespace ServiceControl.AcceptanceTesting.InfrastructureConfig
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using NUnit.Framework;

    public class ConfigureEndpointLearningTransport : ITransportIntegration
    {
        public ConfigureEndpointLearningTransport()
        {
            ConnectionString = Path.Combine(Path.GetTempPath(), "ServiceControlTests", "TestTransport", TestContext.CurrentContext.Test.ID);
        }

        public string ConnectionString { get; set; }

        public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
        {
            Console.WriteLine($"Transport Directory: {ConnectionString}");
            Directory.CreateDirectory(ConnectionString);

            var transportConfig = configuration.UseTransport<LearningTransport>();
            transportConfig.StorageDirectory(ConnectionString);
            transportConfig.NoPayloadSizeRestriction();

            return Task.CompletedTask;
        }

        public Task Cleanup()
        {
            if (Directory.Exists(ConnectionString))
            {
                try
                {
                    Directory.Delete(ConnectionString, true);
                }
                catch (DirectoryNotFoundException)
                {
                }
            }

            return Task.CompletedTask;
        }

        public string Name => "Learning";

        public string TypeName => "LearningTransport";
    }
}