namespace ServiceControl.Transports.ASBS
{
    using System.Linq;
    using System.Text.Json;
    using BrokerThroughput;
    using Configuration;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.Transport.AzureServiceBus;

    public class ASBSTransportCustomization(IConfiguration configuration) : TransportCustomization<AzureServiceBusTransport>
    {
        protected override void CustomizeTransportForPrimaryEndpoint(EndpointConfiguration endpointConfiguration, AzureServiceBusTransport transportDefinition, TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.SendsAtomicWithReceive;

        protected override void CustomizeTransportForAuditEndpoint(EndpointConfiguration endpointConfiguration, AzureServiceBusTransport transportDefinition, TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

        protected override void CustomizeTransportForMonitoringEndpoint(EndpointConfiguration endpointConfiguration, AzureServiceBusTransport transportDefinition, TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

        protected override AzureServiceBusTransport CreateTransport(TransportSettings transportSettings, TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly)
        {
            var connectionSettings = ConnectionStringParser.Parse(transportSettings.ConnectionString);

            if (!transportSettings.TryGet(out TopicTopology selectedTopology))
            {
                //Topology is pre-selected and customized only when creating transport for the primary instance
                //For all other cases use the connection string to determine which topology to use
                if (connectionSettings.TopicName != null)
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    selectedTopology = TopicTopology.MigrateFromNamedSingleTopic(connectionSettings.TopicName);
#pragma warning restore CS0618 // Type or member is obsolete
                }
                else
                {
                    selectedTopology = TopicTopology.Default;
                }
            }

            var transport = connectionSettings.AuthenticationMethod.CreateTransportDefinition(connectionSettings, selectedTopology);
            transport.UseWebSockets = connectionSettings.UseWebSockets;
            transport.EnablePartitioning = connectionSettings.EnablePartitioning;

            transport.TransportTransactionMode = transport.GetSupportedTransactionModes().Contains(preferredTransactionMode) ? preferredTransactionMode : TransportTransactionMode.ReceiveOnly;

            return transport;
        }

        protected override void AddTransportForPrimaryCore(IServiceCollection services,
            TransportSettings transportSettings)
        {
            services.AddSingleton<IBrokerThroughputQuery, AzureQuery>();

            var connectionSettings = ConnectionStringParser.Parse(transportSettings.ConnectionString);
            TopicTopology selectedTopology;

            if (connectionSettings.TopicName != null)
            {
                //Bundle name provided -> use migration topology
                //Need to explicitly specific events to be published on the single topic
#pragma warning disable CS0618 // Type or member is obsolete
                selectedTopology = TopicTopology.FromOptions(new MigrationTopologyOptions
#pragma warning restore CS0618 // Type or member is obsolete
                {
                    TopicToPublishTo = connectionSettings.TopicName,
                    TopicToSubscribeOn = connectionSettings.TopicName,
                    EventsToMigrateMap =
                    [
                        "ServiceControl.Contracts.CustomCheckFailed",
                        "ServiceControl.Contracts.CustomCheckSucceeded",
                        "ServiceControl.Contracts.HeartbeatRestored",
                        "ServiceControl.Contracts.HeartbeatStopped",
                        "ServiceControl.Contracts.FailedMessagesArchived",
                        "ServiceControl.Contracts.FailedMessagesUnArchived",
                        "ServiceControl.Contracts.MessageFailed",
                        "ServiceControl.Contracts.MessageFailureResolvedByRetry",
                        "ServiceControl.Contracts.MessageFailureResolvedManually"
                    ]
                });
            }
            else
            {
                //Load topology from json
                var topologyJson = configuration.GetSection(ServiceBusSectionName).GetValue<string>("Topology");
                selectedTopology = string.IsNullOrWhiteSpace(topologyJson)
                    ? TopicTopology.Default
                    : TopicTopology.FromOptions(JsonSerializer.Deserialize(topologyJson, TopologyOptionsSerializationContext.Default.TopologyOptions));
            }

            transportSettings.Set(selectedTopology);
        }

        protected override void AddTransportForMonitoringCore(IServiceCollection services, TransportSettings transportSettings)
        {
            services.AddSingleton<IProvideQueueLength, QueueLengthProvider>();
            services.AddHostedService(provider => provider.GetRequiredService<IProvideQueueLength>());
        }

        const string ServiceBusSectionName = "ServiceControl.Transport.ASBS";
    }
}