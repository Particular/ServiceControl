using System.Threading.Tasks;
using NServiceBus;
using ServiceBus.Management.AcceptanceTests;

public class ConfigureEndpointAzureServiceBusNetStandardTransport : ITransportIntegration
{
    public Task Configure(string endpointName, EndpointConfiguration configuration)
    {
        configuration.UseSerialization<NewtonsoftSerializer>();

        var transportConfig = configuration.UseTransport<ServiceControl.Transports.AzureServiceBus.AzureServiceBusTransport>();
        
        transportConfig.ConnectionString(ConnectionString);

        return Task.FromResult(0);
    }

    public Task Cleanup()
    {
        return Task.FromResult(0);
    }

    public string TypeName => typeof(ServiceControl.Transports.AzureServiceBus.AzureServiceBusTransport).AssemblyQualifiedName;
    public string ConnectionString { get; set; }
}