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

    public async Task<Endpoint> GetEndpointByNameOrQueue(string nameOrQueue, ThroughputSource throughputSource)
    {
        var endpoint = endpoints.FirstOrDefault(w => w.ThroughputSource == throughputSource && (w.Name == nameOrQueue || w.Queue == nameOrQueue));

        return await Task.FromResult(endpoint);
    }
    public async Task RecordEndpointThroughput(Endpoint endpoint)
    {
        var existingEndpoint = endpoints.FirstOrDefault(w => w.Name == endpoint.Name && w.ThroughputSource == endpoint.ThroughputSource);

        if (existingEndpoint == null)
        {
            endpoints.Add(endpoint);
        }
        else
        {
            if (existingEndpoint.DailyThroughput == null)
            {
                existingEndpoint.DailyThroughput = endpoint.DailyThroughput;
            }
            else if (endpoint.DailyThroughput != null)
            {
                existingEndpoint.DailyThroughput = existingEndpoint.DailyThroughput.Concat(endpoint.DailyThroughput).ToList();
            }
        }

        await Task.CompletedTask;
    }

    public async Task UpdateUserIndicationOnEndpoints(List<Endpoint> endpoints)
    {
        endpoints.ForEach(e =>
        {
            var existingEndpoint = endpoints.FirstOrDefault(w => w.Name == e.Name && w.ThroughputSource == e.ThroughputSource);

            if (existingEndpoint != null)
            {
                existingEndpoint.UserIndicatedSendOnly = e.UserIndicatedSendOnly;
                existingEndpoint.UserIndicatedToIgnore = e.UserIndicatedToIgnore;
            }
        });

        await Task.CompletedTask;
    }

    public Task Setup() => Task.CompletedTask;
}
