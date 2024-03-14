namespace Particular.ThroughputCollector.Persistence
{
    public enum Broker
    {
        None,
        AmazonSQS,
        RabbitMQ,
        AzureServiceBus,
        SqlServer
    }
}
