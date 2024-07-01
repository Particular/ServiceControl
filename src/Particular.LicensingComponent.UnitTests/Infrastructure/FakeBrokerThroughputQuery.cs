namespace Particular.LicensingComponent.UnitTests.Infrastructure;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using ServiceControl.Transports.BrokerThroughput;

class FakeBrokerThroughputQuery : IBrokerThroughputQuery
{
    public Dictionary<string, string> Data => throw new NotImplementedException();

    public string MessageTransport => "AzureServiceBus";

    public string ScopeType => "";

    public KeyDescriptionPair[] Settings => throw new NotImplementedException();

    public IAsyncEnumerable<IBrokerQueue> GetQueueNames(CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IBrokerQueue brokerQueue, DateOnly startDate,
        CancellationToken cancellationToken) => throw new NotImplementedException();

    public bool HasInitialisationErrors(out string errorMessage) => throw new NotImplementedException();
    public void Initialize(ReadOnlyDictionary<string, string> settings) => throw new NotImplementedException();

    public Task<(bool Success, List<string> Errors, string Diagnostics)> TestConnection(
        CancellationToken cancellationToken) => throw new NotImplementedException();

    public string SanitizeEndpointName(string endpointName) => endpointName;
    public string SanitizedEndpointNameCleanser(string endpointName) => endpointName;
}