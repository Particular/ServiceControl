namespace Particular.ThroughputCollector.Persistence.RavenDb;

using Contracts;
using Particular.ThroughputCollector.Persistence.RavenDb.Models;
using Raven.Client.Documents;

class ThroughputDataStore(
    IDocumentStore store,
    PersistenceSettings persistenceSettings,
    DatabaseConfiguration databaseConfiguration) : IThroughputDataStore
{
    const string ThroughputTimeSeriesName = "INC: throughput data";

    public async Task<IEnumerable<Endpoint>> GetAllEndpoints(bool includePlatformEndpoints = true, CancellationToken cancellationToken = default)
    {
        using var session = store.OpenAsyncSession(databaseConfiguration.Name);

        var baseQuery = session.Advanced.AsyncDocumentQuery<EndpointDocument>();

        var query = includePlatformEndpoints
            ? baseQuery
            : baseQuery.Not.ContainsAny(document => document.EndpointId.Name, persistenceSettings.PlatformEndpointNames);

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

    public Task RecordEndpointThroughput(Endpoint endpoint) => throw new NotImplementedException();
    public Task UpdateUserIndicatorOnEndpoints(List<Endpoint> endpointsWithUserIndicator) => throw new NotImplementedException();
    public Task AppendEndpointThroughput(Endpoint endpoint) => throw new NotImplementedException();
    public Task<bool> IsThereThroughputForLastXDays(int days) => throw new NotImplementedException();
    public Task<BrokerData?> GetBrokerData(Broker broker) => throw new NotImplementedException();

    public Task SaveBrokerData(Broker broker, string? scopeType, Dictionary<string, string> data) =>
        throw new NotImplementedException();
}