namespace Particular.ThroughputCollector.Persistence.InMemory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Particular.ThroughputCollector.Contracts;

class InMemoryThroughputDataStore : IThroughputDataStore
{
    private readonly List<Endpoint> endpoints;
    private List<BrokerData> brokerData;

    public InMemoryThroughputDataStore()
    {
        endpoints = [];
        brokerData = [];
    }

    public Task Setup() => Task.CompletedTask;

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
            //ensure we are not adding a date entry more than once
            existingEndpoint.DailyThroughput.AddRange(endpoint.DailyThroughput.Where(w => !existingEndpoint.DailyThroughput.Any(a => a.DateUTC == w.DateUTC)));
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

    public async Task UpdateUserIndicatorOnEndpoints(List<Endpoint> endpointsWithUserIndicator)
    {
        endpointsWithUserIndicator.DistinctBy(b => b.SanitizedName).ToList().ForEach(e =>
        {
            //if there are multiple sources of throughput for the endpoint, update them all
            var existingEndpoints = GetAllEndpointThroughput(e.SanitizedName);

            existingEndpoints.ForEach(u =>
            {
                u.UserIndicator = e.UserIndicator;
            });
        });

        await Task.CompletedTask;
    }

    public async Task<bool> IsThereThroughputForLastXDays(int days) => await Task.FromResult(endpoints.Any(e => e.DailyThroughput.Any(t => t.DateUTC >= DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-days) && t.DateUTC <= DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1))));

    private List<Endpoint> GetAllEndpointThroughput(string name) => endpoints.Where(w => w.SanitizedName == name).ToList();

    public async Task SaveBrokerData(Broker broker, string? scopeType, string? version)
    {
        var existingBrokerData = await GetBrokerData(broker);
        if (existingBrokerData == null)
        {
            existingBrokerData = new BrokerData { Broker = broker };
            brokerData.Add(existingBrokerData);
        }
        existingBrokerData.ScopeType = scopeType ?? existingBrokerData.ScopeType;
        existingBrokerData.Version = version ?? existingBrokerData.Version;

        await Task.CompletedTask;
    }

    public async Task<BrokerData?> GetBrokerData(Broker broker)
    {
        return await Task.FromResult(brokerData.FirstOrDefault(w => w.Broker == broker));
    }
}
