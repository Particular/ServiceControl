namespace ServiceControl.Transports.MT.RabbitMQ;

using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Features;
using NServiceBus.Transport;

public class RabbitMQTransportAdapterCustomization : ITransportCustomization
{
    public void CustomizeAuditEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings) { }

    public void AddTransportForAudit(IServiceCollection services, TransportSettings transportSettings) { }

    public void CustomizeMonitoringEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings) { }

    public void AddTransportForMonitoring(IServiceCollection services, TransportSettings transportSettings) { }

    public void CustomizePrimaryEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
    {
        endpointConfiguration.DisableFeature<Audit>();
        endpointConfiguration.DisableFeature<AutoSubscribe>();
        endpointConfiguration.DisableFeature<Outbox>();
        endpointConfiguration.DisableFeature<Sagas>();
        endpointConfiguration.SendFailedMessagesTo(transportSettings.ErrorQueue);

        if (transportSettings.ConnectionString == null)
        {
            throw new InvalidOperationException("Connection string not configured");
        }

        var transport = new RabbitMQAdapter(transportSettings.ConnectionString) { TransportTransactionMode = TransportTransactionMode.ReceiveOnly };

        endpointConfiguration.UseTransport(transport);
    }

    public void AddTransportForPrimary(IServiceCollection services, TransportSettings transportSettings)
    {
        services.AddSingleton<ITransportCustomization>(this);
        services.AddSingleton(transportSettings);
        //services.AddSingleton<IBrokerThroughputQuery, RabbitMQQuery>();
    }

    public Task ProvisionQueues(TransportSettings transportSettings, IEnumerable<string> additionalQueues) => Task.CompletedTask;

    public Task<TransportInfrastructure> CreateTransportInfrastructure(string name, TransportSettings transportSettings,
        OnMessage onMessage = null,
        OnError onError = null, Func<string, Exception, Task> onCriticalError = null,
        TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly) =>
        //HINT: this is called by primary instance only when message is returned to the endpoint's queueu
        throw new NotImplementedException();
}