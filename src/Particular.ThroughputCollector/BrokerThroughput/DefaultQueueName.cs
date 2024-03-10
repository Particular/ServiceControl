namespace Particular.ThroughputCollector.Broker;

public class DefaultQueueName(string queueName) : IQueueName
{
    public string QueueName { get; } = queueName;
}