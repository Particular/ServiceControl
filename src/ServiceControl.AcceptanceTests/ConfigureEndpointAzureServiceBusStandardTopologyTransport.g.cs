extern alias TransportASBS;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using ServiceBus.Management.AcceptanceTests;

public class ConfigureEndpointAzureServiceBusStandardTransport : ITransportIntegration
{
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        configuration.UseSerialization<NewtonsoftSerializer>();

        // Relies on extern alias at the top of the file to disambiguate AzureServiceBusTransport
        // from NServiceBus.Azure.Transports.WindowsAzureServiceBus. If we need to add extension method calls,
        // include:
        //      using TransportASBS::NServiceBus;
        // in the using statements at the top. Also, in that case, ReSharper will probably throw a fit with
        // the extension method calls but it still compiles.
        var transportConfig = configuration.UseTransport<TransportASBS::NServiceBus.AzureServiceBusTransport>();

        transportConfig.ConnectionString(ConnectionString);

        return Task.FromResult(0);
    }

    public Task Cleanup()
    {
        return Task.FromResult(0);
    }

    public string Name => "AzureServiceBus - .NET Standard";
    public string TypeName => $"{typeof(ServiceControl.Transports.ASBS.ASBSTransportCustomization).AssemblyQualifiedName}";
    public string ConnectionString { get; set; }
}