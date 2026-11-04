namespace ServiceControl.Transports.GooglePubSub;

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using Transport.GooglePubSub;

public class GooglePubSubTransportCustomization : TransportCustomization<GooglePubSubTransport>
{
    protected override void CustomizeTransportForPrimaryEndpoint(EndpointConfiguration endpointConfiguration, GooglePubSubTransport transportDefinition, TransportSettings transportSettings) { }

    protected override void CustomizeTransportForAuditEndpoint(EndpointConfiguration endpointConfiguration, GooglePubSubTransport transportDefinition, TransportSettings transportSettings) { }

    protected override void CustomizeTransportForMonitoringEndpoint(EndpointConfiguration endpointConfiguration, GooglePubSubTransport transportDefinition, TransportSettings transportSettings) { }

    protected override void AddTransportForMonitoringCore(IServiceCollection services, TransportSettings transportSettings)
    {
        services.AddSingleton<IProvideQueueLength, NoOpQueueLengthProvider>();
        services.AddHostedService(provider => provider.GetRequiredService<IProvideQueueLength>());
    }

    public override IEnumerable<TransportEnvironmentDatum> GetEnvironmentData(TransportSettings transportSettings) =>
    [
        new("Transport.GooglePubSub.NamePrefix", () => new GooglePubSubConnectionString(transportSettings.ConnectionString).ResourceNamePrefix is null ? "None" : "Configured")
    ];

    protected override GooglePubSubTransport CreateTransport(TransportSettings transportSettings, TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly)
    {
        var connectionString = new GooglePubSubConnectionString(transportSettings.ConnectionString);

        // Credentials always come from Application Default Credentials. See docs/google-pubsub-transport.md#credentials
        var transport = new GooglePubSubTransport(new GooglePubSubTransportOptions
        {
            ProjectId = connectionString.ProjectId,
            ResourceNamePrefix = connectionString.ResourceNamePrefix,
            ServiceEndpoint = connectionString.ServiceEndpoint
        });

        transport.TransportTransactionMode = transport.GetSupportedTransactionModes().Contains(preferredTransactionMode) ? preferredTransactionMode : TransportTransactionMode.ReceiveOnly;

        return transport;
    }
}