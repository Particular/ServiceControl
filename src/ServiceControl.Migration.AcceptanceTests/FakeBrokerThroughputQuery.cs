namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ServiceControl.Transports.BrokerThroughput;

/// <summary>
/// A broker that reports the daily counts a test stages, only for days on or after the start date the collector asks from,
/// records the start date of every request, and counts how many whole collector passes have read it.
/// </summary>
class FakeBrokerThroughputQuery : IBrokerThroughputQuery
{
    readonly ConcurrentDictionary<string, ConcurrentDictionary<DateOnly, long>> staged = new();
    readonly ConcurrentQueue<(string Queue, DateOnly StartDate)> requests = new();
    int queuesStillReading;
    int completedPasses;

    /// <summary>
    /// How many times the collector has finished reading every staged queue's days.
    /// </summary>
    public int CompletedPasses => Volatile.Read(ref completedPasses);

    /// <summary>
    /// Every queue the collector asked for days of, with the first day it asked for, in the order it asked.
    /// </summary>
    public IReadOnlyList<(string Queue, DateOnly StartDate)> Requests => [.. requests];

    /// <summary>
    /// Makes the broker report count messages on the queue for that day, replacing any count staged for the same day.
    /// </summary>
    public void Stage(string queue, DateOnly day, long count) => staged.GetOrAdd(queue, _ => new())[day] = count;

    public Dictionary<string, string> Data { get; } = [];

    public string MessageTransport => "FakeBroker";

    public string ScopeType => null;

    public KeyDescriptionPair[] Settings => [];

    public void Initialize(ReadOnlyDictionary<string, string> settings) { }

    public bool HasInitialisationErrors(out string errorMessage)
    {
        errorMessage = string.Empty;
        return false;
    }

    public Task<(bool Success, List<string> Errors, string Diagnostics)> TestConnection(CancellationToken cancellationToken = default) =>
        Task.FromResult((true, new List<string>(), "Fake broker reachable"));

    public async IAsyncEnumerable<IBrokerQueue> GetQueueNames([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var queues = staged.Keys.ToArray();
        Volatile.Write(ref queuesStillReading, queues.Length);

        foreach (var queue in queues)
        {
            yield return new DefaultBrokerQueue(queue);
        }

        await Task.CompletedTask;
    }

    public async IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IBrokerQueue brokerQueue, DateOnly startDate, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        requests.Enqueue((brokerQueue.QueueName, startDate));

        try
        {
            foreach (var (day, count) in staged[brokerQueue.QueueName].Where(entry => entry.Key >= startDate).OrderBy(entry => entry.Key))
            {
                yield return new QueueThroughput { DateUTC = day, TotalThroughput = count };
            }
        }
        finally
        {
            // The collector records each day before asking for the next, so the last queue's enumeration ending is the pass ending.
            if (Interlocked.Decrement(ref queuesStillReading) == 0)
            {
                Interlocked.Increment(ref completedPasses);
            }
        }

        await Task.CompletedTask;
    }

    public string SanitizeEndpointName(string endpointName) => endpointName;

    public string SanitizedEndpointNameCleanser(string endpointName) => endpointName;
}
