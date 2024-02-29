namespace Particular.ThroughputCollector.Persistence.RavenDb;

using System.Collections.Generic;
using System.Threading.Tasks;
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

    public Task<Endpoint> GetEndpointByNameOrQueue(string nameOrQueue, ThroughputSource throughputSource) => throw new NotImplementedException();
    public Task RecordEndpointThroughput(Endpoint endpoint) => throw new NotImplementedException();
    public Task UpdateUserIndicationOnEndpoints(List<Endpoint> endpoints) => throw new NotImplementedException();

    readonly IDocumentStore store;
}