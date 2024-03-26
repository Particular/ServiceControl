#nullable enable
namespace ServiceControl.Transports;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading;

public class DefaultQueueName(string queueName) : IQueueName
{
    public string QueueName { get; } = queueName;
    public string? Scope { get; } = null;
    public List<string> EndpointIndicators { get; } = [];
}

public interface IThroughputQuery
{
    void Initialise(FrozenDictionary<string, string> settings);
    IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IQueueName queueName, DateOnly startDate,
        CancellationToken cancellationToken);

    IAsyncEnumerable<IQueueName> GetQueueNames(CancellationToken cancellationToken);
    Dictionary<string, string> Data { get; }
    string MessageTransport { get; }
    string? ScopeType { get; }
    bool SupportsHistoricalMetrics { get; }
    KeyDescriptionPair[] Settings { get; }
}

public readonly struct KeyDescriptionPair(string key, string description)
{
    public string Key { get; } = key;
    public string Description { get; } = description;
}

public class QueueThroughput
{
    public DateOnly DateUTC { get; set; }
    public long TotalThroughput { get; set; }
}

public interface IQueueName
{
    public string QueueName { get; }
    public string? Scope { get; }
    public List<string> EndpointIndicators { get; }
}