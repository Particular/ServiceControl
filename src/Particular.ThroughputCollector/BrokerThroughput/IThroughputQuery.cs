namespace Particular.ThroughputCollector.Broker;

using System.Collections.Frozen;

public interface IThroughputQuery
{
    void Initialise(FrozenDictionary<string, string> settings);

    IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IQueueName queueName, DateTime startDate,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<IQueueName> GetQueueNames(CancellationToken cancellationToken = default);

    string? ScopeType { get; }

    bool SupportsHistoricalMetrics { get; }
}

public class QueueThroughput
{
    public DateTime DateUTC { get; set; }
    public long TotalThroughput { get; set; }
    public string? Scope { get; set; }
    public string[] EndpointIndicators { get; set; } = [];
}

public interface IQueueName
{
    public string QueueName { get; }
}