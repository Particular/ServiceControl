using System.IO;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NUnit.Framework;
using ServiceBus.Management.AcceptanceTests;
using ServiceControl.Transports.Learning;

public class ConfigureEndpointLearningTransport : ITransportIntegration
{
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        Directory.CreateDirectory(ConnectionString);

        var transportConfig = configuration.UseTransport<LearningTransport>();
        transportConfig.StorageDirectory(ConnectionString);

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