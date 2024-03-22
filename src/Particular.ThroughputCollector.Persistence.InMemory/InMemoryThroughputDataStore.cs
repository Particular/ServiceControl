namespace Particular.ThroughputCollector.Persistence.InMemory;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contracts;

class InMemoryThroughputDataStore(PersistenceSettings persistenceSettings) : IThroughputDataStore
{
    private readonly EndpointCollection endpoints = [];
    private readonly List<BrokerData> brokerData = [];

    public Task<IEnumerable<Endpoint>> GetAllEndpoints(bool includePlatformEndpoints = true, CancellationToken cancellationToken = default)
    {
        var filteredEndpoints = includePlatformEndpoints
            ? endpoints
            : endpoints.Where(endpoint => !persistenceSettings.PlatformEndpointNames.Contains(endpoint.Id.Name));

        return Task.FromResult(filteredEndpoints);
    }

    public Task<Endpoint?> GetEndpoint(EndpointIdentifier id, CancellationToken cancellationToken = default)
    {
        endpoints.TryGetValue(id, out var endpoint);
        return Task.FromResult(endpoint);
    }

    public Task<IEnumerable<(EndpointIdentifier, Endpoint?)>> GetEndpoints(IEnumerable<EndpointIdentifier> endpointIds, CancellationToken cancellationToken = default)
    {
        var lookup = (IEnumerable<(EndpointIdentifier, Endpoint?)>)endpointIds.ToDictionary(id => id, id => endpoints.TryGetValue(id, out var endpoint) ? endpoint : null);

        return Task.FromResult(lookup);
    }

    public Task SaveEndpoint(Endpoint endpoint, CancellationToken cancellationToken = default)
    {
        endpoints.Add(endpoint);
        return Task.CompletedTask;
    }

    public async Task RecordEndpointThroughput(EndpointIdentifier id, IEnumerable<EndpointThroughput> throughput, CancellationToken cancellationToken = default)
    {
        if (!endpoints.TryGetValue(id, out var endpoint))
        {
            endpoint = new Endpoint(id)
            {
                DailyThroughput = throughput.ToList()
            };
            endpoints.Add(endpoint);
        }
        else
        {
            //ensure we are not adding a date entry more than once
            endpoint.DailyThroughput.AddRange(throughput.Where(w => !endpoint.DailyThroughput.Any(a => a.DateUTC == w.DateUTC)));
        }
        await Task.CompletedTask;
    }

    public async Task AppendEndpointThroughput(Endpoint endpoint)
    {
        if (!endpoints.TryGetValue(endpoint.Id, out var existingEndpoint))
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

    public async Task SaveBrokerData(Broker broker, string? scopeType, Dictionary<string, string> data)
    {
        var existingBrokerData = await GetBrokerData(broker);
        if (existingBrokerData == null)
        {
            existingBrokerData = new BrokerData { Broker = broker };
            brokerData.Add(existingBrokerData);
        }
        existingBrokerData.ScopeType = scopeType;
        existingBrokerData.Data = data;

        await Task.CompletedTask;
    }

    public async Task<BrokerData?> GetBrokerData(Broker broker)
    {
        return await Task.FromResult(brokerData.FirstOrDefault(w => w.Broker == broker));
    }

    class EndpointCollection : KeyedCollection<EndpointIdentifier, Endpoint>
    {
        protected override EndpointIdentifier GetKeyForItem(Endpoint item) => item.Id;
    }
}
