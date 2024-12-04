namespace Particular.LicensingComponent.Persistence.InMemory;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contracts;

public class InMemoryLicensingDataStore : ILicensingDataStore
{
    readonly EndpointCollection endpoints = [];
    readonly Dictionary<EndpointIdentifier, ThroughputData> allThroughput = [];
    BrokerMetadata brokerMetadata = new(null, []);
    AuditServiceMetadata auditServiceMetadata = new([], []);
    List<string> reportMasks = [];

    public Task<IEnumerable<Endpoint>> GetAllEndpoints(bool includePlatformEndpoints, CancellationToken cancellationToken)
    {
        var filteredEndpoints = includePlatformEndpoints
            ? endpoints
            : endpoints.Where(endpoint => endpoint.EndpointIndicators == null || !endpoint.EndpointIndicators.Any(a => a.Equals(EndpointIndicator.PlatformEndpoint.ToString(), StringComparison.OrdinalIgnoreCase)));
        ;

        return Task.FromResult(filteredEndpoints);
    }

    public Task<Endpoint?> GetEndpoint(EndpointIdentifier id, CancellationToken cancellationToken)
    {
        if (endpoints.TryGetValue(id, out var endpoint))
        {
            if (allThroughput.TryGetValue(id, out var endpointThroughput))
            {
                endpoint.LastCollectedDate = endpointThroughput.LastOrDefault().Key;
            }
        }
        return Task.FromResult(endpoint);
    }

    public Task<IEnumerable<(EndpointIdentifier, Endpoint?)>> GetEndpoints(IList<EndpointIdentifier> endpointIds, CancellationToken cancellationToken)
    {
        var endpointLookup = endpointIds.Select(id => (id, endpoints.TryGetValue(id, out var endpoint) ? endpoint : null));

        foreach (var endpoint in endpointLookup)
        {
            if (endpoint.Item2 != null)
            {
                if (allThroughput.TryGetValue(endpoint.id, out var endpointThroughput))
                {
                    endpoint.Item2.LastCollectedDate = endpointThroughput.LastOrDefault().Key;
                }
            }
        }

        return Task.FromResult(endpointLookup);
    }

    public Task SaveEndpoint(Endpoint endpoint, CancellationToken cancellationToken)
    {
        if (endpoints.TryGetValue(endpoint.Id, out var existingEndpoint))
        {
            endpoints.Remove(existingEndpoint);
        }

        endpoints.Add(endpoint);
        return Task.CompletedTask;
    }

    public Task<IDictionary<string, IEnumerable<ThroughputData>>> GetEndpointThroughputByQueueName(IList<string> queueNames, CancellationToken cancellationToken)
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

    public async Task RecordEndpointThroughput(string endpointName, ThroughputSource throughputSource, IList<EndpointDailyThroughput> throughput, CancellationToken cancellationToken)
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

    public async Task UpdateUserIndicatorOnEndpoints(List<UpdateUserIndicator> userIndicatorUpdates, CancellationToken cancellationToken)
    {
        userIndicatorUpdates.ForEach(e =>
        {
            //if there are multiple sources of throughput for the endpoint, update them all
            var existingEndpoints = GetAllConnectedEndpoints(e.Name);

            existingEndpoints.ForEach(u =>
            {
                u.UserIndicator = e.UserIndicator;
                //for ones that matched on endpoint name, update matching on sanitizedName
                var sanitizedMAtchingEndpoints = GetAllConnectedEndpoints(u.SanitizedName);
                sanitizedMAtchingEndpoints.ForEach(s => s.UserIndicator = e.UserIndicator);
            });
        });

        await Task.CompletedTask;
    }

    public async Task<bool> IsThereThroughputForLastXDays(int days, CancellationToken cancellationToken) => await Task.FromResult(
        allThroughput.Any(endpointThroughput => endpointThroughput.Value.Any(
            t => t.Key >= DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-days) &&
                 t.Key <= DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1))));

    public async Task<bool> IsThereThroughputForLastXDaysForSource(int days, ThroughputSource throughputSource, bool includeToday, CancellationToken cancellationToken)
    {
        var endDate = includeToday ? DateOnly.FromDateTime(DateTime.UtcNow) : DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        return await Task.FromResult(
            allThroughput.Any(
                endpointThroughput => endpointThroughput.Key.ThroughputSource == throughputSource &&
                endpointThroughput.Value.Any(t => t.Key >= DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-days) && t.Key <= endDate)));
    }

    List<Endpoint> GetAllConnectedEndpoints(string name) => endpoints.Where(w => w.SanitizedName == name || w.Id.Name == name).ToList();

    public Task<BrokerMetadata> GetBrokerMetadata(CancellationToken cancellationToken) => Task.FromResult(brokerMetadata);

    public Task SaveBrokerMetadata(BrokerMetadata brokerMetadata, CancellationToken cancellationToken)
    {
        this.brokerMetadata = brokerMetadata;
        return Task.CompletedTask;
    }

    public Task<AuditServiceMetadata> GetAuditServiceMetadata(CancellationToken cancellationToken) => Task.FromResult(auditServiceMetadata);

    public Task SaveAuditServiceMetadata(AuditServiceMetadata auditServiceMetadata, CancellationToken cancellationToken)
    {
        this.auditServiceMetadata = auditServiceMetadata;
        return Task.CompletedTask;
    }

    public Task<List<string>> GetReportMasks(CancellationToken cancellationToken) => Task.FromResult(reportMasks);

    public Task SaveReportMasks(List<string> reportMasks, CancellationToken cancellationToken)
    {
        this.reportMasks = reportMasks;
        return Task.CompletedTask;
    }

    class EndpointCollection : KeyedCollection<EndpointIdentifier, Endpoint>
    {
        protected override EndpointIdentifier GetKeyForItem(Endpoint item) => item.Id;
    }
}