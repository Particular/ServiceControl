﻿using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using ServiceBus.Management.AcceptanceTests;
using ServiceControl.Transports.ASQ;

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

    public string Name => "AzureStorageQueues";
    public string TypeName => $"{typeof(ASQTransportCustomization).AssemblyQualifiedName}";
    public string ConnectionString { get; set; }
}