namespace Particular.ThroughputCollector.Persistence.InMemory;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Particular.ThroughputCollector.Persistence;

class InMemoryThroughputDataStore : IThroughputDataStore
{
    public List<Endpoint> endpoints;

    public InMemoryThroughputDataStore()
    {
        endpoints = [];
    }

    public async Task<IReadOnlyList<Endpoint>> GetAllEndpoints()
    {
        return await Task.FromResult(endpoints);
    }

    public async Task<Endpoint> GetEndpointByNameOrQueue(string nameOrQueue)
    {
        var endpoint = endpoints.FirstOrDefault(w => w.Name == nameOrQueue || w.Queue == nameOrQueue);

        return await Task.FromResult(endpoint);
    }
    public async Task RecordEndpointThroughput(Endpoint endpoint)
    {
        var existingEndpoint = endpoints.FirstOrDefault(w => w.Name == endpoint.Name);

        if (existingEndpoint == null)
        {
            endpoints.Add(endpoint);
        }
        else
        {
            existingEndpoint.ThroughputSource = endpoint.ThroughputSource;
            //TODO should anything else be updated here or only the daily throughput?

            if (existingEndpoint.DailyThroughput == null)
            {
                existingEndpoint.DailyThroughput = endpoint.DailyThroughput;
            }
            else if (endpoint.DailyThroughput != null)
            {
                existingEndpoint.DailyThroughput.Concat(endpoint.DailyThroughput);
            }
        }

        await Task.CompletedTask;
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
