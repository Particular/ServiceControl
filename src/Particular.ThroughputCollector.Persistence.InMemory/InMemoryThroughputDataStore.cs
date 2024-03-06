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

    public async Task<Endpoint> GetEndpointByName(string name, ThroughputSource throughputSource)
    {
        var endpoint = endpoints.FirstOrDefault(w => w.ThroughputSource == throughputSource && w.Name == name);

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

    public async Task UpdateUserIndicationOnEndpoints(List<Endpoint> endpointsWithUserIndicator)
    {
        endpointsWithUserIndicator.DistinctBy(b => b.Name).ToList().ForEach(async e =>
        {
            //if there are multiple sources of throughput for the endpoint, update them all
            var existingEndpoints = await GetAllEndpointThroughput(e.Name).ConfigureAwait(false);

            existingEndpoints.ForEach(u =>
            {
                u.UserIndicatedSendOnly = e.UserIndicatedSendOnly;
                u.UserIndicatedToIgnore = e.UserIndicatedToIgnore;
            });
        });

        await Task.CompletedTask;
    }

    async Task<List<Endpoint>> GetAllEndpointThroughput(string name)
    {
        return await Task.FromResult(endpoints.Where(w => w.Name == name).ToList());
    }

    public Task Setup() => Task.CompletedTask;
}
