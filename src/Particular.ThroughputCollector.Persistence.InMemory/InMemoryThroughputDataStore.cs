namespace Particular.ThroughputCollector.Persistence.InMemory;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

class InMemoryThroughputDataStore : IThroughputDataStore
{
    private readonly List<Endpoint> endpoints;

    public InMemoryThroughputDataStore()
    {
        endpoints = [];
    }

    public async Task<IReadOnlyList<Endpoint>> GetAllEndpoints()
    {
        return await Task.FromResult(endpoints);
    }

    public async Task<Endpoint?> GetEndpointByName(string name, ThroughputSource throughputSource)
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
            existingEndpoint.DailyThroughput.AddRange(endpoint.DailyThroughput);
        }

        await Task.CompletedTask;
    }

    public async Task AppendEndpointThroughput(Endpoint endpoint)
    {
        var existingEndpoint = endpoints.FirstOrDefault(w => w.Name == endpoint.Name && w.ThroughputSource == endpoint.ThroughputSource);

        if (existingEndpoint == null)
        {
            endpoints.Add(endpoint);
        }
        else
        {
            foreach (var endpointThroughput in endpoint.DailyThroughput)
            {
                var existingDailyThroughput = existingEndpoint.DailyThroughput.Find(
                    throughput => throughput.DateUTC == endpointThroughput.DateUTC);
                if (existingDailyThroughput == null)
                {
                    existingEndpoint.DailyThroughput.Add(endpointThroughput);
                }
                else
                {
                    existingDailyThroughput.TotalThroughput += endpointThroughput.TotalThroughput;
                }
            }
        }

        await Task.CompletedTask;
    }

    public async Task UpdateUserIndicationOnEndpoints(List<Endpoint> endpointsWithUserIndicator)
    {
        endpointsWithUserIndicator.DistinctBy(b => b.Name).ToList().ForEach(e =>
        {
            //if there are multiple sources of throughput for the endpoint, update them all
            var existingEndpoints = GetAllEndpointThroughput(e.Name);

            existingEndpoints.ForEach(u =>
            {
                u.UserIndicatedSendOnly = e.UserIndicatedSendOnly;
                u.UserIndicatedToIgnore = e.UserIndicatedToIgnore;
            });
        });

        await Task.CompletedTask;
    }

    private List<Endpoint> GetAllEndpointThroughput(string name) => endpoints.Where(w => w.Name == name).ToList();

    public Task Setup() => Task.CompletedTask;
}
