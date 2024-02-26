namespace Particular.ThroughputCollector.Persistence.InMemory;

using System.Collections.Generic;
using System.Threading.Tasks;
using Particular.ThroughputCollector.Persistence;

class InMemoryThroughputDataStore : IThroughputDataStore
{
    public List<KnownEndpoint> knownEndpoints;

    public InMemoryThroughputDataStore()
    {
        knownEndpoints = [];
    }

    public async Task<QueryResult<IList<KnownEndpoint>>> QueryKnownEndpoints()
    {
        await Task.Delay(1);
        return new QueryResult<IList<KnownEndpoint>>(knownEndpoints, new QueryStatsInfo(string.Empty, knownEndpoints.Count));
    }

    //object TryGet(Dictionary<string, object> metadata, string key)
    //{
    //    if (metadata.TryGetValue(key, out var value))
    //    {
    //        return value;
    //    }

    //    return null;
    //}

    public Task Setup() => Task.CompletedTask;
}
