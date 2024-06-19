namespace ServiceControl.Transports.RabbitMQ
{
    using System;
    using System.Linq;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;

    public abstract class RabbitMQConventionalRoutingTransportCustomization(QueueType queueType)
        : TransportCustomization<RabbitMQTransport>
    {
        protected override void CustomizeTransportForPrimaryEndpoint(EndpointConfiguration endpointConfiguration, RabbitMQTransport transportDefinition, TransportSettings transportSettings) { }

        protected override void CustomizeTransportForAuditEndpoint(EndpointConfiguration endpointConfiguration, RabbitMQTransport transportDefinition, TransportSettings transportSettings) { }

        protected override void CustomizeTransportForMonitoringEndpoint(EndpointConfiguration endpointConfiguration, RabbitMQTransport transportDefinition, TransportSettings transportSettings) { }

        protected override RabbitMQTransport CreateTransport(TransportSettings transportSettings, TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly)
        {
            if (transportSettings.ConnectionString == null)
            {
                throw new InvalidOperationException("Connection string not configured");
            }

            var transport = new RabbitMQTransport(RoutingTopology.Conventional(queueType), transportSettings.ConnectionString);
            transport.TransportTransactionMode = transport.GetSupportedTransactionModes().Contains(preferredTransactionMode) ? preferredTransactionMode : TransportTransactionMode.ReceiveOnly;

            return transport;
        }

        protected sealed override void AddTransportForMonitoringCore(IServiceCollection services, TransportSettings transportSettings)
        {
            services.AddSingleton<IProvideQueueLength, QueueLengthProvider>();
            services.AddSingleton<IBrokerThroughputQuery, RabbitMQQuery>();
            services.AddHostedService(provider => provider.GetRequiredService<IProvideQueueLength>());
        }
    }
}