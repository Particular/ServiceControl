namespace ServiceControlInstaller.Engine.Instances
{
    public class TransportNames
    {
        public const string AzureServiceBus = "Azure Service Bus";

        public const string AzureServiceBusEndpointOrientedTopology = "Azure Service Bus - Endpoint-oriented topology (Legacy)";

        public const string AzureServiceBusForwardingTopology = "Azure Service Bus - Forwarding topology (Legacy)";

        public const string AmazonSQS = "AmazonSQS";

        public const string AzureStorageQueue = "Azure Storage Queue";

        public const string MSMQ = "MSMQ";

        public const string SQLServer = "SQL Server";

        public const string RabbitMQConventionalRoutingTopology = "RabbitMQ - Conventional routing topology";

        public const string RabbitMQDirectRoutingTopology = "RabbitMQ - Direct routing topology (Old)";

        TransportNames() { }
    }
}