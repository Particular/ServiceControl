using System.IO;
using System.Threading.Tasks;
using NServiceBus;
using NUnit.Framework;
using ServiceBus.Management.AcceptanceTests;

public class ConfigureEndpointLearningTransport : ITransportIntegration
{
    public Task Configure(string endpointName, EndpointConfiguration configuration)
    {
        var connectionString = Path.Combine(ConnectionString, ".learningtransport");

        Directory.CreateDirectory(connectionString);

        var transportConfig = configuration.UseTransport<LearningTransport>();
        transportConfig.StorageDirectory(connectionString);

        return Task.FromResult(0);
    }

    public Task Cleanup()
    {
        var connectionString = Path.Combine(ConnectionString, ".learningtransport");

        if (Directory.Exists(connectionString))
        {
            Directory.Delete(connectionString, true);
        }

        return Task.FromResult(0);
    }

    public string MonitoringSeamTypeName => $"{typeof(ServiceControlLearningTransport).AssemblyQualifiedName}";
    public string ConnectionString { get; set; } = Path.Combine(TestContext.CurrentContext.TestDirectory, @"..\..\..\");
}