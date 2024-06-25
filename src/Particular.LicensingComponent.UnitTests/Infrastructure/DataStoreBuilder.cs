namespace Particular.LicensingComponent.UnitTests.Infrastructure;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Contracts;
using Persistence;

class DataStoreBuilder(ILicensingDataStore store)
{
    static readonly Random rng = new();

    readonly List<Endpoint> endpoints = [];
    readonly Dictionary<EndpointIdentifier, List<ThroughputData>> endpointThroughput = [];

    public DataStoreBuilder AddEndpoint(string name = null, IEnumerable<ThroughputSource> sources = null)
    {
        var index = endpoints.Count;

        name ??= Guid.NewGuid().ToString("N");
        sources ??= [ThroughputSource.Broker];
        foreach (var source in sources)
        {
            endpoints.Add(CreateEndpoint(index, name, source));
        }

        return this;
    }

    public DataStoreBuilder ConfigureEndpoint(Action<Endpoint> configureEndpoint) => ConfigureEndpoint(null, configureEndpoint);

    public DataStoreBuilder ConfigureEndpoint(ThroughputSource? source = null, Action<Endpoint> configureEndpoint = null)
    {
        Func<Endpoint, bool> predicate = source is null
            ? endpoint => true
            : endpoint => endpoint.Id.ThroughputSource == source.Value;

        var endpoint = endpoints.LastOrDefault(predicate) ?? throw new InvalidOperationException($"Need to add an endpoint before calling {nameof(ConfigureEndpoint)}");

        configureEndpoint?.Invoke(endpoint);

        return this;
    }

    public DataStoreBuilder WithThroughput(ThroughputSource? source = null, DateOnly? startDate = null, int? days = null, IList<long> data = null)
    {
        var endpoint = endpoints.LastOrDefault() ?? throw new InvalidOperationException($"Need to add an endpoint before calling {nameof(WithThroughput)}");

        if (source is not null)
        {
            var endpointForSource = endpoints.SingleOrDefault(e => e.Id.Name == endpoint.Id.Name && e.Id.ThroughputSource == source);
            if (endpointForSource is null)
            {
                endpointForSource = new Endpoint(endpoint.Id.Name, source.Value) { SanitizedName = endpoint.SanitizedName };
                endpoints.Add(endpointForSource);
            }

            endpoint = endpointForSource;
        }

        source ??= endpoint.Id.ThroughputSource;
        if (endpoints.SingleOrDefault(e => e.Id.Name == endpoint.Id.Name && e.Id.ThroughputSource == source) == null)
        {
            throw new InvalidOperationException($"Need to add endpoint {endpoint.Id.Name}:{source} before calling {nameof(WithThroughput)}");
        }

        var idForThroughput = new EndpointIdentifier(endpoint.Id.Name, source.Value);

        var throughput = CreateThroughput(source.Value, startDate, days, data);

        if (endpointThroughput.TryGetValue(idForThroughput, out var throughputList))
        {
            throughputList.Add(throughput);
        }
        else
        {
            endpointThroughput.Add(idForThroughput, [throughput]);
        }

        return this;
    }

    public async Task Build()
    {
        foreach (var endpoint in endpoints)
        {
            await store.SaveEndpoint(endpoint, default);
        };

        foreach (var (endpointId, throughputList) in endpointThroughput)
        {
            foreach (var throughput in throughputList)
            {
                await store.RecordEndpointThroughput(endpointId.Name, throughput.ThroughputSource, throughput.Select(entry => new EndpointDailyThroughput(entry.Key, entry.Value)).ToList(), default);
            }
        }
    }

    static Endpoint CreateEndpoint(int index, string name, ThroughputSource source)
    {
        string endpointName = name ?? $"Endpoint{index + 1}";

        return new Endpoint(endpointName, source) { SanitizedName = endpointName };
    }

    protected static ThroughputData CreateThroughput(ThroughputSource source, DateOnly? startDate = null, int? days = null, IList<long> data = null)
    {
        var numberOfThroughputEntries = days is not null && data is not null
            ? Math.Max(days.Value, data.Count)
            : days ?? data?.Count ?? 1;

        if (startDate == null)
        {
            startDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-numberOfThroughputEntries);
        }

        var throughput = Enumerable.Range(0, numberOfThroughputEntries)
            .Select(i =>
            {
                var messageCount = data is not null && i < data.Count
                    ? data[i]
                    : rng.Next(100);

                return new EndpointDailyThroughput(startDate.Value.AddDays(i), messageCount);
            });

        return new ThroughputData(throughput) { ThroughputSource = source };
    }
}
