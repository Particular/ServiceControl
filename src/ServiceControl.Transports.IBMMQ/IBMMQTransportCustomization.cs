namespace ServiceControl.Transports.IBMMQ;

using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using NServiceBus.Transport.IbmMq;

public class IBMMQTransportCustomization : TransportCustomization<IbmMqTransport>
{
    protected override void CustomizeTransportForPrimaryEndpoint(EndpointConfiguration endpointConfiguration, IbmMqTransport transportDefinition, TransportSettings transportSettings) =>
        transportDefinition.TransportTransactionMode = TransportTransactionMode.SendsAtomicWithReceive;

    protected override void CustomizeTransportForAuditEndpoint(EndpointConfiguration endpointConfiguration, IbmMqTransport transportDefinition, TransportSettings transportSettings) =>
        transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

    protected override void CustomizeTransportForMonitoringEndpoint(EndpointConfiguration endpointConfiguration, IbmMqTransport transportDefinition, TransportSettings transportSettings) =>
        transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

    protected override void AddTransportForMonitoringCore(IServiceCollection services, TransportSettings transportSettings)
    {
        services.AddSingleton<IProvideQueueLength, NoOpQueueLengthProvider>();
        services.AddHostedService(provider => provider.GetRequiredService<IProvideQueueLength>());
    }

    protected override IbmMqTransport CreateTransport(TransportSettings transportSettings, TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly)
    {
        var transport = new IbmMqTransport(TestConnectionDetails.Apply);
        transport.TransportTransactionMode = transport.GetSupportedTransactionModes().Contains(preferredTransactionMode)
            ? preferredTransactionMode
            : TransportTransactionMode.ReceiveOnly;

        return transport;
    }
}