using System;
using System.Threading.Tasks;
using NServiceBus;
using ServiceBus.Management.AcceptanceTests;

public class ConfigureEndpointAzureStorageQueueTransport : ITransportIntegration
{
    public Task Configure(string endpointName, EndpointConfiguration configuration)
    {
        var transportConfig = configuration
            .UseTransport<AzureStorageQueueTransport>()
            .ConnectionString(ConnectionString)
            .MessageInvisibleTime(TimeSpan.FromSeconds(30));

        transportConfig.SanitizeQueueNamesWith(ServiceControl.Transports.AzureStorageQueues.QueueNameSanitizer.Sanitize);

        transportConfig.DelayedDelivery().DisableTimeoutManager();

        var routingConfig = transportConfig.Routing();

       

        return Task.FromResult(0);
    }

    public Task Cleanup()
    {
        return Task.FromResult(0);
    }

    public string MonitoringSeamTypeName => $"{typeof(ServiceControl.Transports.AzureStorageQueues.ServiceControlAzureStorageQueueTransport).AssemblyQualifiedName}";
    public string ConnectionString { get; set; }
}