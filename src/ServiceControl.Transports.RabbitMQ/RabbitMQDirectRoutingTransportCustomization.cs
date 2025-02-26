namespace ServiceControl.Transports.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using BrokerThroughput;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;

    public abstract class RabbitMQDirectRoutingTransportCustomization(QueueType queueType)
        : TransportCustomization<RabbitMQTransport>, IRabbitMQTransportExtensions
    {
        RabbitMQTransport rabbitMQTransport;

        protected override void CustomizeTransportForPrimaryEndpoint(EndpointConfiguration endpointConfiguration, RabbitMQTransport transportDefinition, TransportSettings transportSettings) { }

        protected override void CustomizeTransportForAuditEndpoint(EndpointConfiguration endpointConfiguration, RabbitMQTransport transportDefinition, TransportSettings transportSettings) { }

        protected override void CustomizeTransportForMonitoringEndpoint(EndpointConfiguration endpointConfiguration, RabbitMQTransport transportDefinition, TransportSettings transportSettings) { }
        protected override RabbitMQTransport CreateTransport(TransportSettings transportSettings, TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly)
        {
            if (transportSettings.ConnectionString == null)
            {
                throw new InvalidOperationException("Connection string not configured");
            }

            var connectionConfiguration = ConnectionConfiguration.Create(transportSettings.ConnectionString, string.Empty);
            var connectionStringDictionary = ConnectionConfiguration.ParseNServiceBusConnectionString(transportSettings.ConnectionString, new StringBuilder());

            var ValidateDeliveryLimitsString = GetValue(connectionStringDictionary, "ValidateDeliveryLimits", "true");
            if (!bool.TryParse(ValidateDeliveryLimitsString, out var validateDeliveryLimits))
            {
                throw new ArgumentException("The value for 'ValidateDeliveryLimits' must be either 'true' or 'false'");
            }

            var transport = new RabbitMQTransport(RoutingTopology.Direct(queueType, routingKeyConvention: type => type.FullName.Replace(".", "-")), transportSettings.ConnectionString, enableDelayedDelivery: false);
            transport.TransportTransactionMode = transport.GetSupportedTransactionModes().Contains(preferredTransactionMode) ? preferredTransactionMode : TransportTransactionMode.ReceiveOnly;
            transport.ValidateDeliveryLimits = validateDeliveryLimits;

            var url = GetValue(connectionStringDictionary, "ManagementApiUrl", string.Empty);

            if (!string.IsNullOrEmpty(url))
            {
                var username = GetValue(connectionStringDictionary, "ManagementApiUserName", connectionConfiguration.UserName);
                var password = GetValue(connectionStringDictionary, "ManagementApiPassword", connectionConfiguration.Password);
                transport.ManagementApiConfiguration = !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password)
                    ? new ManagementApiConfiguration(url, username, password) : new ManagementApiConfiguration(url);
            }

            rabbitMQTransport = transport;
            return transport;
        }

        protected override void AddTransportForPrimaryCore(IServiceCollection services, TransportSettings transportSettings)
            => services.AddSingleton<IBrokerThroughputQuery, RabbitMQQuery>();

        protected sealed override void AddTransportForMonitoringCore(IServiceCollection services, TransportSettings transportSettings)
        {
            services.AddSingleton<IProvideQueueLength, QueueLengthProvider>();
            services.AddHostedService(provider => provider.GetRequiredService<IProvideQueueLength>());
        }

        static string GetValue(Dictionary<string, string> dictionary, string key, string defaultValue)
            => dictionary.TryGetValue(key, out var value) ? value : defaultValue;

        RabbitMQTransport IRabbitMQTransportExtensions.GetTransport()
        {
            if (rabbitMQTransport == null)
            {
                throw new InvalidOperationException("Transport instance has not been created yet. Make sure CreateTransport() is called before accessing the transport.");
            }

            return rabbitMQTransport;
        }
    }
}