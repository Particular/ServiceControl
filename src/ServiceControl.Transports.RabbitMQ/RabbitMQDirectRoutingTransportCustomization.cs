namespace ServiceControl.Transports.RabbitMQ
{
    using System;
    using System.Linq;
    using BrokerThroughput;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.Transport.RabbitMQ.ManagementApi;

    public abstract class RabbitMQDirectRoutingTransportCustomization(NServiceBus.QueueType queueType) : TransportCustomization<RabbitMQTransport>, IManagementClientProvider
    {
        RabbitMQTransport rabbitMQTransport;

        ManagementClient IManagementClientProvider.ManagementClient => rabbitMQTransport?.ManagementClient ?? throw new InvalidOperationException("Transport instance has not been created yet.");

        protected override void CustomizeTransportForPrimaryEndpoint(EndpointConfiguration endpointConfiguration, RabbitMQTransport transportDefinition, TransportSettings transportSettings) => rabbitMQTransport = transportDefinition;

        protected override void CustomizeTransportForAuditEndpoint(EndpointConfiguration endpointConfiguration, RabbitMQTransport transportDefinition, TransportSettings transportSettings) { }

        protected override void CustomizeTransportForMonitoringEndpoint(EndpointConfiguration endpointConfiguration, RabbitMQTransport transportDefinition, TransportSettings transportSettings) => rabbitMQTransport = transportDefinition;

        protected override RabbitMQTransport CreateTransport(TransportSettings transportSettings, TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly)
        {
            if (transportSettings.ConnectionString is null)
            {
                throw new InvalidOperationException("Connection string not configured");
            }

            var transport = new RabbitMQTransport(RoutingTopology.Direct(queueType, routingKeyConvention: type => type.FullName.Replace(".", "-")), transportSettings.ConnectionString, enableDelayedDelivery: false);
            transport.TransportTransactionMode = transport.GetSupportedTransactionModes().Contains(preferredTransactionMode) ? preferredTransactionMode : TransportTransactionMode.ReceiveOnly;
            transport.SetCustomSettingsFromConnectionString(transportSettings.ConnectionString);

            return transport;
        }

        protected override void AddTransportForPrimaryCore(IServiceCollection services, TransportSettings transportSettings)
            => services.AddSingleton<IBrokerThroughputQuery, RabbitMQQuery>();

        protected sealed override void AddTransportForMonitoringCore(IServiceCollection services, TransportSettings transportSettings)
        {
            services.AddSingleton<IProvideQueueLength, QueueLengthProvider>();
            services.AddHostedService(provider => provider.GetRequiredService<IProvideQueueLength>());
        }
    }
}