namespace ServiceControl.Transports.MT.RabbitMQ;

using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Transport;

public class RabbitMQTransportAdapter : ITransportCustomization
{
    public void CustomizePrimaryEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings) => throw new NotImplementedException();

    public void AddTransportForPrimary(IServiceCollection services, TransportSettings transportSettings) => throw new NotImplementedException();

    public void CustomizeAuditEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings) => throw new NotImplementedException();

    public void AddTransportForAudit(IServiceCollection services, TransportSettings transportSettings) => throw new NotImplementedException();

    public void CustomizeMonitoringEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings) => throw new NotImplementedException();

    public void AddTransportForMonitoring(IServiceCollection services, TransportSettings transportSettings) => throw new NotImplementedException();

    public Task ProvisionQueues(TransportSettings transportSettings, IEnumerable<string> additionalQueues) => throw new NotImplementedException();

    public Task<TransportInfrastructure> CreateTransportInfrastructure(string name, TransportSettings transportSettings, OnMessage onMessage = null,
        OnError onError = null, Func<string, Exception, Task> onCriticalError = null,
        TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly) =>
        throw new NotImplementedException();
}