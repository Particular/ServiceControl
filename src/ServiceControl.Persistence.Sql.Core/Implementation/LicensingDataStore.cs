namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Particular.LicensingComponent.Contracts;
using Particular.LicensingComponent.Persistence;
using ServiceControl.Persistence.Sql.Core.Entities;

public class LicensingDataStore : DataStoreBase, ILicensingDataStore
{
    public LicensingDataStore(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }
    #region Throughput
    static DateOnly DefaultCutOff()
        => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-400));

    public Task<IDictionary<string, IEnumerable<ThroughputData>>> GetEndpointThroughputByQueueName(IList<string> queueNames, CancellationToken cancellationToken)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var cutOff = DefaultCutOff();

            var data = await dbContext.Throughput
                .AsNoTracking()
                .Where(x => queueNames.Contains(x.EndpointName) && x.Date >= cutOff)
                .ToListAsync(cancellationToken);

            var lookup = data.ToLookup(x => x.EndpointName);

            Dictionary<string, IEnumerable<ThroughputData>> result = [];

            foreach (var queueName in queueNames)
            {
                result[queueName] = [.. lookup[queueName].GroupBy(x => x.ThroughputSource)
                    .Select(x => new ThroughputData([.. from t in x select new EndpointDailyThroughput(t.Date, t.MessageCount)])
                    {
                        ThroughputSource = Enum.Parse<ThroughputSource>(x.Key)
                    })];
            }

            return (IDictionary<string, IEnumerable<ThroughputData>>)result;
        });
    }

    public Task<bool> IsThereThroughputForLastXDays(int days, CancellationToken cancellationToken)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var cutoffDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days + 1));
            return await dbContext.Throughput.AnyAsync(t => t.Date >= cutoffDate, cancellationToken);
        });
    }

    public Task<bool> IsThereThroughputForLastXDaysForSource(int days, ThroughputSource throughputSource, bool includeToday, CancellationToken cancellationToken)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var cutoffDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days + 1));
            var endDate = DateOnly.FromDateTime(includeToday ? DateTime.UtcNow : DateTime.UtcNow.AddDays(-1));
            var source = Enum.GetName(throughputSource)!;
            return await dbContext.Throughput.AnyAsync(t => t.Date >= cutoffDate && t.Date <= endDate && t.ThroughputSource == source, cancellationToken);
        });
    }

    public Task RecordEndpointThroughput(string endpointName, ThroughputSource throughputSource, IList<EndpointDailyThroughput> throughput, CancellationToken cancellationToken)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var source = Enum.GetName(throughputSource)!;
            var cutOff = DefaultCutOff();
            var existing = await dbContext.Throughput.Where(t => t.EndpointName == endpointName && t.ThroughputSource == source && t.Date >= cutOff)
                .ToListAsync(cancellationToken);

            var lookup = existing.ToLookup(t => t.Date);

            foreach (var t in throughput)
            {
                var existingEntry = lookup[t.DateUTC].FirstOrDefault();
                if (existingEntry is not null)
                {
                    existingEntry.MessageCount = t.MessageCount;
                }
                else
                {
                    var newEntry = new DailyThroughputEntity
                    {
                        EndpointName = endpointName,
                        ThroughputSource = source,
                        Date = t.DateUTC,
                        MessageCount = t.MessageCount,
                    };
                    await dbContext.Throughput.AddAsync(newEntry, cancellationToken);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        });
    }
    #endregion

    #region Endpoints
    public Task<IEnumerable<(EndpointIdentifier Id, Endpoint? Endpoint)>> GetEndpoints(IList<EndpointIdentifier> endpointIds, CancellationToken cancellationToken)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var fromDatabase = await dbContext.Endpoints.AsNoTracking()
                .Where(e => endpointIds.Any(id => id.Name == e.EndpointName && Enum.GetName(id.ThroughputSource) == e.ThroughputSource))
                .ToListAsync(cancellationToken);

            var lookup = fromDatabase.Select(MapEndpointEntityToContract).ToLookup(e => e.Id);

            return endpointIds.Select(id => (id, lookup[id].FirstOrDefault()));
        });
    }

    public Task<Endpoint?> GetEndpoint(EndpointIdentifier id, CancellationToken cancellationToken = default)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var fromDatabase = await dbContext.Endpoints.AsNoTracking().SingleOrDefaultAsync(e => e.EndpointName == id.Name && e.ThroughputSource == Enum.GetName(id.ThroughputSource), cancellationToken);
            if (fromDatabase is null)
            {
                return null;
            }

            return MapEndpointEntityToContract(fromDatabase);
        });
    }

    public Task<IEnumerable<Endpoint>> GetAllEndpoints(bool includePlatformEndpoints, CancellationToken cancellationToken)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var endpoints = dbContext.Endpoints.AsNoTracking();
            if (!includePlatformEndpoints)
            {
                endpoints = endpoints.Where(x => x.EndpointIndicators == null || !x.EndpointIndicators.Contains(Enum.GetName(EndpointIndicator.PlatformEndpoint)!));
            }

            var fromDatabase = await endpoints.ToListAsync(cancellationToken);

            return fromDatabase.Select(MapEndpointEntityToContract);
        });
    }

    public Task SaveEndpoint(Endpoint endpoint, CancellationToken cancellationToken)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var existing = await dbContext.Endpoints.SingleOrDefaultAsync(e => e.EndpointName == endpoint.Id.Name && e.ThroughputSource == Enum.GetName(endpoint.Id.ThroughputSource), cancellationToken);
            if (existing is null)
            {
                var entity = MapEndpointContractToEntity(endpoint);
                await dbContext.Endpoints.AddAsync(entity, cancellationToken);
            }
            else
            {
                existing.SanitizedEndpointName = endpoint.SanitizedName;
                existing.EndpointIndicators = endpoint.EndpointIndicators is null ? null : string.Join("|", endpoint.EndpointIndicators);
                existing.UserIndicator = endpoint.UserIndicator;
                existing.Scope = endpoint.Scope;
                existing.LastCollectedData = endpoint.LastCollectedDate;
                dbContext.Endpoints.Update(existing);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        });
    }

    public Task UpdateUserIndicatorOnEndpoints(List<UpdateUserIndicator> userIndicatorUpdates, CancellationToken cancellationToken)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var updates = userIndicatorUpdates.ToDictionary(u => u.Name, u => u.UserIndicator);

            // Get all relevant sanitized names from endpoints matched by name
            var sanitizedNames = await dbContext.Endpoints
                .Where(e => updates.Keys.Contains(e.EndpointName) && e.SanitizedEndpointName != null)
                .Select(e => e.SanitizedEndpointName)
                .Distinct()
                .ToListAsync(cancellationToken);

            // Get all endpoints that match either by name or sanitized name in a single query
            var endpoints = await dbContext.Endpoints
                    .Where(e => updates.Keys.Contains(e.EndpointName)
                        || (e.SanitizedEndpointName != null && updates.Keys.Contains(e.SanitizedEndpointName))
                        || (e.SanitizedEndpointName != null && sanitizedNames.Contains(e.SanitizedEndpointName)))
                    .ToListAsync(cancellationToken) ?? [];

            foreach (var endpoint in endpoints)
            {
                if (endpoint.SanitizedEndpointName is not null && updates.TryGetValue(endpoint.SanitizedEndpointName, out var newValueFromSanitizedName))
                {
                    // Direct match by sanitized name
                    endpoint.UserIndicator = newValueFromSanitizedName;
                }
                else if (updates.TryGetValue(endpoint.EndpointName, out var newValueFromEndpoint))
                {
                    // Direct match by endpoint name - this should also update all endpoints with the same sanitized name
                    endpoint.UserIndicator = newValueFromEndpoint;
                }
                else if (endpoint.SanitizedEndpointName != null && sanitizedNames.Contains(endpoint.SanitizedEndpointName))
                {
                    // This endpoint shares a sanitized name with an endpoint that was matched by name
                    // Find the update value from the endpoint that has this sanitized name
                    var matchingEndpoint = endpoints.FirstOrDefault(e =>
                        e.SanitizedEndpointName == endpoint.SanitizedEndpointName &&
                        updates.ContainsKey(e.EndpointName));

                    if (matchingEndpoint != null && updates.TryGetValue(matchingEndpoint.EndpointName, out var cascadedValue))
                    {
                        endpoint.UserIndicator = cascadedValue;
                    }
                }
                dbContext.Endpoints.Update(endpoint);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        });
    }


    static Endpoint MapEndpointEntityToContract(ThroughputEndpointEntity entity)
    => new(entity.EndpointName, Enum.Parse<ThroughputSource>(entity.ThroughputSource))
    {
#pragma warning disable CS8601 // Possible null reference assignment.
        SanitizedName = entity.SanitizedEndpointName,
        EndpointIndicators = entity.EndpointIndicators?.Split("|"),
        UserIndicator = entity.UserIndicator,
        Scope = entity.Scope,
        LastCollectedDate = entity.LastCollectedData
#pragma warning restore CS8601 // Possible null reference assignment.
    };

    static ThroughputEndpointEntity MapEndpointContractToEntity(Endpoint endpoint)
        => new()
        {
            EndpointName = endpoint.Id.Name,
            ThroughputSource = Enum.GetName(endpoint.Id.ThroughputSource)!,
            SanitizedEndpointName = endpoint.SanitizedName,
            EndpointIndicators = endpoint.EndpointIndicators is null ? null : string.Join("|", endpoint.EndpointIndicators),
            UserIndicator = endpoint.UserIndicator,
            Scope = endpoint.Scope,
            LastCollectedData = endpoint.LastCollectedDate
        };


    #endregion

    #region AuditServiceMetadata

    static readonly AuditServiceMetadata EmptyAuditServiceMetadata = new([], []);
    public Task SaveAuditServiceMetadata(AuditServiceMetadata auditServiceMetadata, CancellationToken cancellationToken)
        => SaveMetadata("AuditServiceMetadata", auditServiceMetadata, cancellationToken);
    public async Task<AuditServiceMetadata> GetAuditServiceMetadata(CancellationToken cancellationToken = default)
        => await LoadMetadata<AuditServiceMetadata>("AuditServiceMetadata", cancellationToken)
           ?? EmptyAuditServiceMetadata;

    #endregion

    #region ReportMasks
    static readonly List<string> EmptyReportMasks = [];
    public Task SaveReportMasks(List<string> reportMasks, CancellationToken cancellationToken)
        => SaveMetadata("ReportMasks", reportMasks, cancellationToken);
    public async Task<List<string>> GetReportMasks(CancellationToken cancellationToken)
        => await LoadMetadata<List<string>>("ReportMasks", cancellationToken) ?? EmptyReportMasks;

    #endregion

    #region Broker Metadata
    static readonly BrokerMetadata EmptyBrokerMetada = new(ScopeType: null, []);

    public Task SaveBrokerMetadata(BrokerMetadata brokerMetadata, CancellationToken cancellationToken)
        => SaveMetadata("BrokerMetadata", brokerMetadata, cancellationToken);

    public async Task<BrokerMetadata> GetBrokerMetadata(CancellationToken cancellationToken)
        => await LoadMetadata<BrokerMetadata>("BrokerMetadata", cancellationToken) ?? EmptyBrokerMetada;
    #endregion

    #region Metadata
    Task<T?> LoadMetadata<T>(string key, CancellationToken cancellationToken)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var existing = await dbContext.LicensingMetadata
                .AsNoTracking()
                .SingleOrDefaultAsync(m => m.Key == key, cancellationToken);
            if (existing is null)
            {
                return default;
            }
            return JsonSerializer.Deserialize<T>(existing.Data);
        });
    }

    Task SaveMetadata<T>(string key, T data, CancellationToken cancellationToken)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var existing = await dbContext.LicensingMetadata.SingleOrDefaultAsync(m => m.Key == key, cancellationToken);

            var serialized = JsonSerializer.Serialize(data);

            if (existing is null)
            {
                LicensingMetadataEntity newMetadata = new()
                {
                    Key = key,
                    Data = serialized
                };
                await dbContext.LicensingMetadata.AddAsync(newMetadata, cancellationToken);
            }
            else
            {
                existing.Data = serialized;
                dbContext.LicensingMetadata.Update(existing);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        });
    }
    #endregion
}
