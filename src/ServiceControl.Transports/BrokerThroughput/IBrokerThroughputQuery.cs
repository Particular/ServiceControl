#nullable enable
namespace ServiceControl.Transports.BrokerThroughput;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IBrokerThroughputQuery
{
    bool HasInitialisationErrors(out string errorMessage);
    void Initialise(FrozenDictionary<string, string> settings);
    IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IBrokerQueue brokerQueue, DateOnly startDate,
        CancellationToken cancellationToken);
    IAsyncEnumerable<IBrokerQueue> GetQueueNames(CancellationToken cancellationToken);
    Dictionary<string, string> Data { get; }
    string MessageTransport { get; }
    string? ScopeType { get; }
    KeyDescriptionPair[] Settings { get; }
    Task<(bool Success, List<string> Errors, string Diagnostics)> TestConnection(CancellationToken cancellationToken);
    string SanitizeEndpointName(string endpointName);
    string SanitizedEndpointNameCleanser(string endpointName);
}