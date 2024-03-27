namespace Particular.ThroughputCollector.Persistence.InMemory;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contracts;

class InMemoryThroughputDataStore() : IThroughputDataStore
{
    private readonly EndpointCollection endpoints = [];
    private readonly Dictionary<EndpointIdentifier, ThroughputData> allThroughput = [];
    private readonly List<BrokerData> brokerData = [];

    public Task<IEnumerable<Endpoint>> GetAllEndpoints(bool includePlatformEndpoints = true, CancellationToken cancellationToken = default)
    {
        var filteredEndpoints = includePlatformEndpoints
            ? endpoints
            : endpoints.Where(endpoint => endpoint.EndpointIndicators == null || !endpoint.EndpointIndicators.Any(a => a.Equals(EndpointIndicator.PlatformEndpoint.ToString(), StringComparison.OrdinalIgnoreCase)));
        ;

        return Task.FromResult(filteredEndpoints);
    }

    public Task<Endpoint?> GetEndpoint(EndpointIdentifier id, CancellationToken cancellationToken = default)
    {
        endpoints.TryGetValue(id, out var endpoint);
        return Task.FromResult(endpoint);
    }

    public Task<IEnumerable<(EndpointIdentifier, Endpoint?)>> GetEndpoints(IEnumerable<EndpointIdentifier> endpointIds, CancellationToken cancellationToken = default)
    {
        var endpointLookup = endpointIds.Select(id => (id, endpoints.TryGetValue(id, out var endpoint) ? endpoint : null));

        return Task.FromResult(endpointLookup);
    }

    public Task SaveEndpoint(Endpoint endpoint, CancellationToken cancellationToken = default)
    {
        if (endpoints.TryGetValue(endpoint.Id, out var existingEndpoint))
        {
            endpoints.Remove(existingEndpoint);
        }

        endpoints.Add(endpoint);
        return Task.CompletedTask;
    }

    public Task<IDictionary<string, IEnumerable<ThroughputData>>> GetEndpointThroughputByQueueName(IEnumerable<string> queueNames, CancellationToken cancellationToken = default)
    {
        var result = endpoints
            .Where(endpoint => queueNames.Contains(endpoint.SanitizedName))
            .Join(allThroughput,
                endpoint => endpoint.Id,
                throughputDictionary => throughputDictionary.Key,
                (endpoint, throughputDictionary) => new { endpoint.SanitizedName, throughputDictionary.Value })
            .GroupBy(anon => anon.SanitizedName)
            .ToDictionary(group => group.Key, group => group.Select(entry => entry.Value));

        return Task.FromResult((IDictionary<string, IEnumerable<ThroughputData>>)result);
    }

    public async Task RecordEndpointThroughput(string endpointName, ThroughputSource throughputSource, IEnumerable<EndpointDailyThroughput> throughput, CancellationToken cancellationToken = default)
    {
        var id = new EndpointIdentifier(endpointName, throughputSource);
        if (!endpoints.TryGetValue(id, out _))
        {
            throw new InvalidOperationException($"Endpoint {id.Name} from {id.ThroughputSource} does not exist ");
        }

        if (!allThroughput.TryGetValue(id, out var endpointThroughput))
        {
            endpointThroughput = new ThroughputData { ThroughputSource = id.ThroughputSource };
            allThroughput.Add(id, endpointThroughput);
        }

        foreach (var (date, messageCount) in throughput)
        {
            var newCount = messageCount;

            if (endpointThroughput.TryGetValue(date, out var existingCount))
            {
                newCount += existingCount;
            }

            endpointThroughput[date] = newCount;
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

    public async Task<bool> IsThereThroughputForLastXDays(int days) => await Task.FromResult(
        allThroughput.Any(endpointThroughput => endpointThroughput.Value.Any(
            t => t.Key >= DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-days) &&
                 t.Key <= DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1))));

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
