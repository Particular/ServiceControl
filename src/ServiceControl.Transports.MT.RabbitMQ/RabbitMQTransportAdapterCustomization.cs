namespace ServiceControl.Transports.MT.RabbitMQ;

using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Faults;
using NServiceBus.Features;
using NServiceBus.Transport;

public class RabbitMQTransportAdapterCustomization : ITransportCustomization
{
    public void CustomizePrimaryEndpoint(EndpointConfiguration endpointConfiguration,
        TransportSettings transportSettings)
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

        var transport = new RabbitMQTransport(RoutingTopology.Conventional(QueueType.Quorum), transportSettings.ConnectionString) { TransportTransactionMode = TransportTransactionMode.ReceiveOnly };

        endpointConfiguration.UseTransport(transport);
    }

    public void AddTransportForPrimary(IServiceCollection services, TransportSettings transportSettings)
    {
        services.AddSingleton<ITransportCustomization>(this);
        services.AddSingleton(transportSettings);
    }

    public void CustomizeAuditEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings) => throw new NotImplementedException();

    public void AddTransportForAudit(IServiceCollection services, TransportSettings transportSettings) => throw new NotImplementedException();

    public void CustomizeMonitoringEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings) => throw new NotImplementedException();

    public void AddTransportForMonitoring(IServiceCollection services, TransportSettings transportSettings) => throw new NotImplementedException();

    public Task ProvisionQueues(TransportSettings transportSettings, IEnumerable<string> additionalQueues) => throw new NotImplementedException();

    public async Task<TransportInfrastructure> CreateTransportInfrastructure(string name, TransportSettings transportSettings,
        OnMessage onMessage = null,
        OnError onError = null, Func<string, Exception, Task> onCriticalError = null,
        TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly)
    {
        //HINT: this is called by primary instance only when message is returned to the endpoint's queueu

        if (transportSettings.ConnectionString == null)
        {
            throw new InvalidOperationException("Connection string not configured");
        }

        var transport = new RabbitMQTransport(RoutingTopology.Conventional(QueueType.Classic), transportSettings.ConnectionString);
        transport.TransportTransactionMode = transport.GetSupportedTransactionModes().Contains(preferredTransactionMode) ? preferredTransactionMode : TransportTransactionMode.ReceiveOnly;

        onCriticalError ??= (_, __) => Task.CompletedTask;

        var hostSettings = new HostSettings(
            name,
            $"TransportInfrastructure for {name}",
            new StartupDiagnosticEntries(),
            (msg, exception, cancellationToken) => Task.Run(() => onCriticalError(msg, exception), cancellationToken),
            false,
            null); //null means "not hosted by core", transport SHOULD adjust accordingly to not assume things


        ReceiveSettings[] receivers;
        var createReceiver = onMessage != null && onError != null;

        if (createReceiver)
        {
            if (name == "error")
            {
                //HACK: This is a hack!!!!
                receivers = [new ReceiveSettings("error", new QueueAddress("FailWhenReceivingMyMessage_error"), false, false, transportSettings.ErrorQueue)];
                onMessage = AdaptOnMessage(onMessage);

                OnMessage AdaptOnMessage(OnMessage baseOnMessage)
                {
                    return (context, token) =>
                    {
                        MassTransitConverter.From(context);
                        return baseOnMessage(context, token);
                    };
                }
            }
            else
            {
                receivers = [new ReceiveSettings(name, new QueueAddress(name), false, false, transportSettings.ErrorQueue)];
            }

        }
        else
        {
            receivers = [];
        }

        var transportInfrastructure = await transport.Initialize(hostSettings, receivers, new[] { transportSettings.ErrorQueue }).ConfigureAwait(false);

        if (createReceiver)
        {
            var transportInfrastructureReceiver = transportInfrastructure.Receivers[name];
            await transportInfrastructureReceiver.Initialize(new PushRuntimeSettings(transportSettings.MaxConcurrency), onMessage, onError, CancellationToken.None).ConfigureAwait(false);
        }

        return transportInfrastructure;
    }
}