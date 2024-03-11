namespace Particular.ThroughputCollector.Broker;

using System.Collections.Frozen;
using Persistence;

public interface IThroughputQuery
{
    void Initialise(FrozenDictionary<string, string> settings);

    IAsyncEnumerable<EndpointThroughput> GetThroughputPerDay(IQueueName queueName, DateTime startDate,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<IQueueName> GetQueueNames(CancellationToken cancellationToken = default);
}

public interface IQueueName
{
    public string QueueName { get; }
}