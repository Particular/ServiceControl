namespace ServiceControl.Transports.IBMMQ;

using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using NServiceBus.Transport.IBMMQ;

public class IBMMQTransportCustomization : TransportCustomization<IBMMQTransport>
{
    protected override void CustomizeTransportForPrimaryEndpoint(EndpointConfiguration endpointConfiguration, IBMMQTransport transportDefinition, TransportSettings transportSettings) =>
        transportDefinition.TransportTransactionMode = TransportTransactionMode.SendsAtomicWithReceive;

    protected override void CustomizeTransportForAuditEndpoint(EndpointConfiguration endpointConfiguration, IBMMQTransport transportDefinition, TransportSettings transportSettings) =>
        transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

    protected override void CustomizeTransportForMonitoringEndpoint(EndpointConfiguration endpointConfiguration, IBMMQTransport transportDefinition, TransportSettings transportSettings) =>
        transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

    protected override void AddTransportForMonitoringCore(IServiceCollection services, TransportSettings transportSettings)
    {
        services.AddSingleton<IProvideQueueLength, QueueLengthProvider>();
        services.AddHostedService(provider => provider.GetRequiredService<IProvideQueueLength>());
    }

    protected override IBMMQTransport CreateTransport(TransportSettings transportSettings, TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly)
    {
        var transport = new IBMMQTransport(o =>
        {
            if (transportSettings.TryGet<Action<IBMMQTransportOptions>>(out var overrides))
            {
                overrides(o);
            }

            TestConnectionDetails.Apply(o);
        });
        transport.TransportTransactionMode = transport.GetSupportedTransactionModes().Contains(preferredTransactionMode)
            ? preferredTransactionMode
            : TransportTransactionMode.ReceiveOnly;

        return transport;
    }
}