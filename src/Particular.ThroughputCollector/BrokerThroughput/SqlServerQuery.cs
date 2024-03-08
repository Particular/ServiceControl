namespace Particular.ThroughputCollector.Broker;

using System.Collections.Frozen;
using Persistence;

public class SqlServerQuery : IThroughputQuery
{
    public void Initialise(FrozenDictionary<string, string> settings)
    {
    }

    public IAsyncEnumerable<EndpointThroughput> GetThroughputPerDay(string queueName, DateTime startDate,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public IAsyncEnumerable<string> GetQueueNames(CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}