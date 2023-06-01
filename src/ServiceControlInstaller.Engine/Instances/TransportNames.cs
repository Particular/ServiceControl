namespace ServiceControlInstaller.Engine.Instances
{
    public static class TransportNames
    {
        public const string AzureServiceBus = "Azure Service Bus";

        public const string DeprecatedPrefix = "DEPRECATED: ";

        public const string AzureServiceBusEndpointOrientedTopologyDeprecated = DeprecatedPrefix + "Azure Service Bus - Endpoint-oriented topology (Legacy)";

        public const string AzureServiceBusEndpointOrientedTopologyLegacy = "Azure Service Bus - Endpoint-oriented topology (Legacy)";
        // for backward compatibility
        public const string AzureServiceBusEndpointOrientedTopologyOld = "Azure Service Bus - Endpoint-oriented topology (Old)";

        public const string AzureServiceBusForwardingTopologyDeprecated = DeprecatedPrefix + "Azure Service Bus - Forwarding topology (Legacy)";

        public const string AzureServiceBusForwardingTopologyLegacy = "Azure Service Bus - Forwarding topology (Legacy)";
        // for backward compatibility
        public const string AzureServiceBusForwardingTopologyOld = "Azure Service Bus - Forwarding topology (Old)";

        public const string AmazonSQS = "AmazonSQS";

        public const string AzureStorageQueue = "Azure Storage Queue";

        public const string MSMQ = "MSMQ";

        public const string SQLServer = "SQL Server";

        public const string RabbitMQConventionalRoutingTopologyDeprecated = "RabbitMQ - Conventional routing topology";

        public const string RabbitMQClassicConventionalRoutingTopology = "RabbitMQ - Conventional routing topology (classic queues)";

        public const string RabbitMQQuorumConventionalRoutingTopology = "RabbitMQ - Conventional routing topology (quorum queues)";

        public const string RabbitMQDirectRoutingTopologyDeprecated = "RabbitMQ - Direct routing topology (Old)";

        public const string RabbitMQClassicDirectRoutingTopology = "RabbitMQ - Direct routing topology (classic queues)";

        public const string RabbitMQQuorumDirectRoutingTopology = "RabbitMQ - Direct routing topology (quorum queues)";
        public const string LearningTransport = "Learning";

        public static bool IsDeprecated(string transportName)
        {
            return transportName is AzureServiceBusEndpointOrientedTopologyDeprecated or
                   AzureServiceBusEndpointOrientedTopologyLegacy or
                   AzureServiceBusEndpointOrientedTopologyOld or
                   AzureServiceBusForwardingTopologyDeprecated or
                   AzureServiceBusForwardingTopologyLegacy or
                   AzureServiceBusForwardingTopologyOld or
                   RabbitMQConventionalRoutingTopologyDeprecated;
        }
    }
}