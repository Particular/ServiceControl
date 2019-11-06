using System.IO;
using System.Threading.Tasks;
using NServiceBus;
using NUnit.Framework;
using ServiceBus.Management.AcceptanceTests;
using ServiceControl.Transports.Learning;

public class ConfigureEndpointLearningTransport : ITransportIntegration
{
    public Task Configure(string endpointName, EndpointConfiguration configuration)
    {
        var connectionString = ConnectionString;

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

    public string MonitoringSeamTypeName => $"{typeof(LearningTransportCustomization).AssemblyQualifiedName}";
    public string ConnectionString { get; set; } = Path.Combine(TestContext.CurrentContext.TestDirectory, @"..\..\..\");
}