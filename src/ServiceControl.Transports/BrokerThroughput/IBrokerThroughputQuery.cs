#nullable enable
namespace ServiceControl.Transports.BrokerThroughput;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

public interface IBrokerThroughputQuery
{
    bool HasInitialisationErrors(out string errorMessage);
    void Initialize(ReadOnlyDictionary<string, string> settings);
    IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IBrokerQueue brokerQueue, DateOnly startDate,
        CancellationToken cancellationToken = default);
    IAsyncEnumerable<IBrokerQueue> GetQueueNames(CancellationToken cancellationToken = default);
    Dictionary<string, string> Data { get; }
    string MessageTransport { get; }
    string? ScopeType { get; }
    KeyDescriptionPair[] Settings { get; }
    Task<(bool Success, List<string> Errors, string Diagnostics)> TestConnection(CancellationToken cancellationToken = default);
    string SanitizeEndpointName(string endpointName);
    string SanitizedEndpointNameCleanser(string endpointName);
}