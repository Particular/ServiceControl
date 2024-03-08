namespace Particular.ThroughputCollector.Broker;

using System.Collections.Frozen;
using Persistence;

public interface IThroughputQuery
{
    void Initialise(FrozenDictionary<string, string> settings);

    IAsyncEnumerable<EndpointThroughput> GetThroughputPerDay(string queueName, DateTime startDate,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> GetQueueNames(CancellationToken cancellationToken = default);
}