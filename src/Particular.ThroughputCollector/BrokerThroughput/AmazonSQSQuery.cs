namespace Particular.ThroughputCollector.Broker;

using System.Collections.Frozen;
using Persistence;

public class AmazonSQSQuery : IThroughputQuery
{
    public void Initialise(FrozenDictionary<string, string> settings) => throw new NotImplementedException();

    public IAsyncEnumerable<EndpointThroughput> GetThroughputPerDay(IQueueName queueName, DateTime startDate,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public IAsyncEnumerable<IQueueName> GetQueueNames(CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}