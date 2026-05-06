namespace ServiceControl.Transports.ASBS
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Azure.Messaging.ServiceBus.Administration;
    using BrokerThroughput;
    using Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.Transport.AzureServiceBus;

    public class ASBSTransportCustomization : TransportCustomization<AzureServiceBusTransport>
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
            if (!string.IsNullOrWhiteSpace(connectionSettings.HierarchyNamespace))
            {
                transport.HierarchyNamespaceOptions = new HierarchyNamespaceOptions { HierarchyNamespace = connectionSettings.HierarchyNamespace };
            }

            transport.TransportTransactionMode = transport.GetSupportedTransactionModes().Contains(preferredTransactionMode) ? preferredTransactionMode : TransportTransactionMode.ReceiveOnly;

            return transport;
        }

        protected override void AddTransportForPrimaryCore(IServiceCollection services,
            TransportSettings transportSettings)
        {
            services.AddSingleton<IBrokerThroughputQuery, AzureQuery>();

            var connectionSettings = ConnectionStringParser.Parse(transportSettings.ConnectionString);
            TopicTopology selectedTopology;

            var serviceBusRootNamespace = new SettingsRootNamespace("ServiceControl.Transport.ASBS");
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
                    EventsToMigrateMap = [.. transportSettings.EventTypesPublished.Select(t => t.FullName)]
                });
            }
            else if (SettingsReader.TryRead<string>(serviceBusRootNamespace, "Topology", out var topologyJson))
            {
                //Load topology from json
                selectedTopology = TopicTopology.FromOptions(JsonSerializer.Deserialize(topologyJson, TopologyOptionsSerializationContext.Default.TopologyOptions));
            }
            else
            {
                //Default to topic-per-event topology
                selectedTopology = TopicTopology.Default;
            }

            transportSettings.Set(selectedTopology);
        }

        protected override void AddTransportForMonitoringCore(IServiceCollection services, TransportSettings transportSettings)
        {
            services.AddSingleton<IProvideQueueLength, QueueLengthProvider>();
            services.AddHostedService(provider => provider.GetRequiredService<IProvideQueueLength>());
        }

        public override async Task ProvisionQueues(TransportSettings transportSettings, IEnumerable<string> additionalQueues)
        {
            await base.ProvisionQueues(transportSettings, additionalQueues);

            if (transportSettings.EventTypesPublished.Count == 0)
            {
                return;
            }

            var connectionSettings = ConnectionStringParser.Parse(transportSettings.ConnectionString);

            var managementClient = connectionSettings.AuthenticationMethod.BuildManagementClient();

            var creationTasks = new List<Task>(transportSettings.EventTypesPublished.Count);
            foreach (var publishedTopic in transportSettings.EventTypesPublished)
            {
                creationTasks.Add(CreateTopic(publishedTopic.FullName));
            }
            await Task.WhenAll(creationTasks);

            async Task CreateTopic(string publishedTopic)
            {
                var topicToPublishTo = new CreateTopicOptions(connectionSettings.HierarchyNamespace != null
                    ? $"{connectionSettings.HierarchyNamespace}/{publishedTopic}"
                    : publishedTopic)
                {
                    EnableBatchedOperations = true,
                    MaxSizeInMegabytes = 5 * 1024, // we are currently not configuring this in the connection string so it uses the same default as the transport
                    EnablePartitioning = connectionSettings.EnablePartitioning,
                };

                try
                {
                    await managementClient.CreateTopicAsync(topicToPublishTo).ConfigureAwait(false);
                }
                catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists || sbe.IsTransient)
                {
                    // carry on
                }
            }
        }
    }
}