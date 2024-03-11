namespace Particular.ThroughputCollector.Persistence.RavenDb;

using Raven.Client.Documents;

class ThroughputDataStore : IThroughputDataStore
{
    public ThroughputDataStore(IDocumentStore store)
    {
        this.store = store;
    }

    public async Task<IReadOnlyList<Endpoint>> GetAllEndpoints()
    {
        using var session = store.OpenAsyncSession();

        var endpoints = await session.Query<Endpoint, EndpointIndex>().ToListAsync().ConfigureAwait(false);

        return endpoints.ToArray();
    }

    public Task<Endpoint?> GetEndpointByName(string name, ThroughputSource throughputSource) =>
        throw new NotImplementedException();
    public Task RecordEndpointThroughput(Endpoint endpoint) => throw new NotImplementedException();
    public Task UpdateUserIndicationOnEndpoints(List<Endpoint> endpointsWithUserIndicator) => throw new NotImplementedException();
    public Task AppendEndpointThroughput(Endpoint endpoint) => throw new NotImplementedException();

    readonly IDocumentStore store;
}