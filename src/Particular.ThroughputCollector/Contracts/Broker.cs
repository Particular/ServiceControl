namespace Particular.ThroughputCollector.Contracts
{
    public enum Broker
    {
        None,
        AmazonSQS,
        RabbitMQ,
        AzureServiceBus,
        AzureStorageQueues,
        SqlServer
    }
}
