﻿namespace Particular.ThroughputCollector.Persistence.RavenDb;

using Contracts;
using Particular.ThroughputCollector.Persistence.RavenDb.Models;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;

class ThroughputDataStore(
    IDocumentStore store,
    DatabaseConfiguration databaseConfiguration) : IThroughputDataStore
{
    const string ThroughputTimeSeriesName = "INC: throughput data";

    public async Task<IEnumerable<Endpoint>> GetAllEndpoints(bool includePlatformEndpoints = true, CancellationToken cancellationToken = default)
    {
        using var session = store.OpenAsyncSession(databaseConfiguration.Name);

        var baseQuery = session.Query<EndpointDocument>();

        var query = includePlatformEndpoints
            ? baseQuery
            : baseQuery.Where(document => !document.EndpointIndicators.Contains(EndpointIndicator.PlatformEndpoint.ToString()));

        var documents = await query.ToListAsync(cancellationToken);

        return documents.Select(document => document.ToEndpoint());
    }

    public async Task<Endpoint?> GetEndpoint(EndpointIdentifier id, CancellationToken cancellationToken = default)
    {
        using var session = store.OpenAsyncSession(databaseConfiguration.Name);

        var documentId = id.GenerateDocumentId();

        var document = await session.LoadAsync<EndpointDocument>(
            documentId,
            builder => builder.IncludeTimeSeries(ThroughputTimeSeriesName),
            cancellationToken);

        var endpoint = document?.ToEndpoint();
        if (endpoint != null)
        {
            var timeSeries = await session
                .IncrementalTimeSeriesFor(documentId, ThroughputTimeSeriesName)
                .GetAsync(token: cancellationToken);

            endpoint.LastCollectedDate = DateOnly.FromDateTime(timeSeries.LastOrDefault()?.Timestamp ?? DateTime.MinValue);
        }

        return endpoint;
    }

    public async Task<IEnumerable<(EndpointIdentifier, Endpoint?)>> GetEndpoints(IEnumerable<EndpointIdentifier> endpointIds, CancellationToken cancellationToken = default)
    {
        var endpoints = new List<(EndpointIdentifier, Endpoint?)>();

        using var session = store.OpenAsyncSession(databaseConfiguration.Name);

        var documentIdLookup = endpointIds.ToDictionary(
            endpointId => endpointId,
            endpointId => endpointId.GenerateDocumentId());

        var endpointDocuments = await session.LoadAsync<EndpointDocument>(
            documentIdLookup.Values.Distinct(),
            builder => builder.IncludeTimeSeries(ThroughputTimeSeriesName),
            cancellationToken);

        foreach (var (documentId, endpointDocument) in endpointDocuments)
        {
            var id = endpointDocument == null
             ? endpointIds.First(id => id.GenerateDocumentId().Equals(documentId))
             : endpointDocument.EndpointId;

            var endpoint = endpointDocument?.ToEndpoint();
            if (endpoint != null)
            {
                var timeSeries = await session
                    .IncrementalTimeSeriesFor(documentId, ThroughputTimeSeriesName)
                    .GetAsync(token: cancellationToken);

                endpoint.LastCollectedDate = DateOnly.FromDateTime(timeSeries.LastOrDefault()?.Timestamp ?? DateTime.MinValue);
            }

            endpoints.Add((id, endpoint));
        }

        return endpoints;
    }

    public async Task SaveEndpoint(Endpoint endpoint, CancellationToken cancellationToken = default)
    {
        var document = endpoint.ToEndpointDocument();

        using var session = store.OpenAsyncSession("throughput");

        await session.StoreAsync(document, document.GenerateDocumentId(), cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }

    public Task<IDictionary<string, IEnumerable<ThroughputData>>> GetEndpointThroughputByQueueName(IEnumerable<string> queueNames, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public async Task RecordEndpointThroughput(string endpointName, ThroughputSource throughputSource, IEnumerable<EndpointDailyThroughput> throughput, CancellationToken cancellationToken = default)
    {
        if (!throughput.Any())
        {
            return;
        }

        var id = new EndpointIdentifier(endpointName, throughputSource);
        using var session = store.OpenAsyncSession("throughput");

        var documentId = id.GenerateDocumentId();
        var document = await session.LoadAsync<EndpointDocument>(documentId, cancellationToken) ??
            throw new InvalidOperationException($"Endpoint {id.Name} from {id.ThroughputSource} does not exist ");

        var timeSeries = session.IncrementalTimeSeriesFor(documentId, ThroughputTimeSeriesName);

        foreach (var (date, messageCount) in throughput)
        {
            timeSeries.Increment(date.ToDateTime(TimeOnly.MinValue), messageCount);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    public Task UpdateUserIndicatorOnEndpoints(List<Endpoint> endpointsWithUserIndicator) => throw new NotImplementedException();

    public Task<bool> IsThereThroughputForLastXDays(int days) => throw new NotImplementedException();
    public Task<bool> IsThereThroughputForLastXDaysForSource(int days, ThroughputSource throughputSource) => throw new NotImplementedException();
    public Task<BrokerData?> GetBrokerData(Broker broker) => throw new NotImplementedException();

    public Task SaveBrokerData(Broker broker, string? scopeType, Dictionary<string, string> data) =>
        throw new NotImplementedException();
}