namespace Particular.ThroughputCollector.Persistence.RavenDb;

using Raven.Client.Documents;

class ThroughputDataStore(IDocumentStore store, DatabaseConfiguration databaseConfiguration) : IThroughputDataStore
{
    public async Task<IReadOnlyList<Endpoint>> GetAllEndpoints()
    {
        using var session = store.OpenAsyncSession(databaseConfiguration.Name);

        var endpoints = await session.Query<Endpoint, EndpointIndex>().ToListAsync().ConfigureAwait(false);

        return endpoints.ToArray();
    }

    public Task<Endpoint?> GetEndpointByName(string name, ThroughputSource throughputSource) =>
        throw new NotImplementedException();
    public Task RecordEndpointThroughput(Endpoint endpoint) => throw new NotImplementedException();
    public Task UpdateUserIndicatorOnEndpoints(List<Endpoint> endpointsWithUserIndicator) => throw new NotImplementedException();
    public Task AppendEndpointThroughput(Endpoint endpoint) => throw new NotImplementedException();
    public Task<bool> IsThereThroughputForLastXDays(int days) => throw new NotImplementedException();
    public Task<BrokerData?> GetBrokerData(Broker broker) => throw new NotImplementedException();
    public Task SaveBrokerData(Broker broker, string? scopeType, string? Version) => throw new NotImplementedException();
}