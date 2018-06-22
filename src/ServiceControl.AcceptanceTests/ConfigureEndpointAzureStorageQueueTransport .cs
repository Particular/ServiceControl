using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using ServiceBus.Management.AcceptanceTests;
using ServiceControl.Infrastructure;

public class ConfigureEndpointAzureStorageQueueTransport : ITransportIntegration
{
    public string Name => "AzureStorageQueues";
    public string TypeName => "NServiceBus.AzureStorageQueueTransport, NServiceBus.Azure.Transports.WindowsAzureStorageQueues";
    public string ConnectionString { get; set; }
    
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {

        var transportConfig = configuration
            .UseTransport<AzureStorageQueueTransport>()
            .ConnectionString(ConnectionString)
            .MessageInvisibleTime(TimeSpan.FromSeconds(30));
        
        transportConfig.SanitizeQueueNamesWith(AsqBackwardsCompatibleQueueNameSanitizer.Sanitize);

        transportConfig.DelayedDelivery().DisableTimeoutManager();

        var routingConfig = transportConfig.Routing();

        foreach (var publisher in publisherMetadata.Publishers)
        {
            foreach (var eventType in publisher.Events)
            {
                routingConfig.RegisterPublisher(eventType, publisher.PublisherName);
            }
        }
        
        return Task.FromResult(0);
    }

    public Task Cleanup()
    {
        return Task.FromResult(0);
    }
}