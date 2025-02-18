namespace ServiceControl.Transports.ASBS
{
    using System.Linq;
    using System.Text.Json;
    using BrokerThroughput;
    using Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.Transport.AzureServiceBus;

    public class ASBSTransportCustomization : TransportCustomization<AzureServiceBusTransport>
    {
        const string DefaultSingleTopic = "bundle-1";

        protected override void CustomizeTransportForPrimaryEndpoint(EndpointConfiguration endpointConfiguration, AzureServiceBusTransport transportDefinition, TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.SendsAtomicWithReceive;

        protected override void CustomizeTransportForAuditEndpoint(EndpointConfiguration endpointConfiguration, AzureServiceBusTransport transportDefinition, TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

        protected override void CustomizeTransportForMonitoringEndpoint(EndpointConfiguration endpointConfiguration, AzureServiceBusTransport transportDefinition, TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

        protected override AzureServiceBusTransport CreateTransport(TransportSettings transportSettings, TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly)
        {
            var connectionSettings = ConnectionStringParser.Parse(transportSettings.ConnectionString);
            TopologyOptions topologyOptions;

            var serviceBusRootNamespace = new SettingsRootNamespace("ServiceControl.Transport.ASBS");
            if (SettingsReader.TryRead<string>(serviceBusRootNamespace, "Topology", out var topologyJson))
            {
                topologyOptions = JsonSerializer.Deserialize<TopologyOptions>(topologyJson);
            }
            else
            {
                var options = new MigrationTopologyOptions
                {
                    TopicToPublishTo = connectionSettings.TopicName ?? DefaultSingleTopic,
                    TopicToSubscribeOn = connectionSettings.TopicName ?? DefaultSingleTopic,
                    PublishedEventToTopicsMap =
                    {
                        ["ServiceControl.Contracts.CustomCheckFailed"] = "ServiceControl.Contracts.CustomCheckFailed",
                        ["ServiceControl.Contracts.CustomCheckSucceeded"] = "ServiceControl.Contracts.CustomCheckSucceeded",
                        ["ServiceControl.Contracts.HeartbeatRestored"] = "ServiceControl.Contracts.HeartbeatRestored",
                        ["ServiceControl.Contracts.HeartbeatStopped"] = "ServiceControl.Contracts.HeartbeatStopped",
                        ["ServiceControl.Contracts.FailedMessagesArchived"] = "ServiceControl.Contracts.FailedMessagesArchived",
                        ["ServiceControl.Contracts.FailedMessagesUnArchived"] = "ServiceControl.Contracts.FailedMessagesUnArchived",
                        ["ServiceControl.Contracts.MessageFailed"] = "ServiceControl.Contracts.MessageFailed",
                        ["ServiceControl.Contracts.MessageFailureResolvedByRetry"] = "ServiceControl.Contracts.MessageFailureResolvedByRetry",
                        ["ServiceControl.Contracts.MessageFailureResolvedManually"] = "ServiceControl.Contracts.MessageFailureResolvedManually"
                    }
                };
                topologyOptions = options;
            }

            var transport = connectionSettings.AuthenticationMethod.CreateTransportDefinition(connectionSettings, TopicTopology.FromOptions(topologyOptions));
            transport.UseWebSockets = connectionSettings.UseWebSockets;
            transport.EnablePartitioning = connectionSettings.EnablePartitioning;

            transport.TransportTransactionMode = transport.GetSupportedTransactionModes().Contains(preferredTransactionMode) ? preferredTransactionMode : TransportTransactionMode.ReceiveOnly;

            return transport;
        }

        protected override void AddTransportForPrimaryCore(IServiceCollection services,
            TransportSettings transportSettings)
        {
            services.AddSingleton<IBrokerThroughputQuery, AzureQuery>();
        }

        protected override void AddTransportForMonitoringCore(IServiceCollection services, TransportSettings transportSettings)
        {
            services.AddSingleton<IProvideQueueLength, QueueLengthProvider>();
            services.AddHostedService(provider => provider.GetRequiredService<IProvideQueueLength>());
        }
    }
}