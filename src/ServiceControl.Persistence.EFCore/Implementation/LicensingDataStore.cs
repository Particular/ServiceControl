namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Particular.LicensingComponent.Contracts;
using Particular.LicensingComponent.Persistence;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

class LicensingDataStore(IServiceScopeFactory scopeFactory, TimeProvider timeProvider) : DataStoreBase(scopeFactory), ILicensingDataStore
{
    // A report covers the last 14 months, so older throughput is never read.
    const int ReportedMonths = 14;
    const int MaxRecordAttempts = 5;

    static readonly string PlatformEndpointIndicator = EndpointIndicator.PlatformEndpoint.ToString();
    static readonly AuditServiceMetadata DefaultAuditServiceMetadata = new([], []);
    static readonly BrokerMetadata DefaultBrokerMetadata = new(null, []);

    public Task<IEnumerable<Endpoint>> GetAllEndpoints(bool includePlatformEndpoints, CancellationToken cancellationToken) =>
        ExecuteWithDbContext(async context =>
        {
            var query = context.LicensingEndpoints.AsNoTracking();

            if (!includePlatformEndpoints)
            {
                query = query.Where(endpoint => !endpoint.EndpointIndicators.Contains(PlatformEndpointIndicator));
            }

            var rows = await query.ToListAsync(cancellationToken);

            return rows.Select(ToEndpoint);
        });

    public Task<Endpoint?> GetEndpoint(EndpointIdentifier id, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async context =>
        {
            var normalizedName = Normalize(id.Name);

            var row = await context.LicensingEndpoints
                .AsNoTracking()
                .Where(endpoint => endpoint.NormalizedName == normalizedName && endpoint.ThroughputSource == id.ThroughputSource)
                .Select(endpoint => new
                {
                    Endpoint = endpoint,
                    LastCollectedDate = context.LicensingEndpointThroughput
                        .Where(row => row.NormalizedName == endpoint.NormalizedName && row.ThroughputSource == endpoint.ThroughputSource)
                        .Max<LicensingEndpointThroughputEntity, DateOnly?>(row => row.DateUtc)
                })
                .SingleOrDefaultAsync(cancellationToken);

            return row is null ? null : ToEndpoint(row.Endpoint, row.LastCollectedDate);
        });

    public Task<IEnumerable<(EndpointIdentifier Id, Endpoint? Endpoint)>> GetEndpoints(IList<EndpointIdentifier> endpointIds, CancellationToken cancellationToken) =>
        ExecuteWithDbContext(async context =>
        {
            var normalizedNames = endpointIds.Select(id => Normalize(id.Name)).Distinct().ToList();

            // Every requested endpoint in one round trip, then matched up in memory, because the ids
            // can span several sources of the same name.
            var rows = await context.LicensingEndpoints
                .AsNoTracking()
                .Where(endpoint => normalizedNames.Contains(endpoint.NormalizedName))
                .Select(endpoint => new
                {
                    Endpoint = endpoint,
                    LastCollectedDate = context.LicensingEndpointThroughput
                        .Where(row => row.NormalizedName == endpoint.NormalizedName && row.ThroughputSource == endpoint.ThroughputSource)
                        .Max<LicensingEndpointThroughputEntity, DateOnly?>(row => row.DateUtc)
                })
                .ToListAsync(cancellationToken);

            var found = rows.ToDictionary(row => (row.Endpoint.NormalizedName, row.Endpoint.ThroughputSource));

            return endpointIds
                .Select(id => (id, found.TryGetValue((Normalize(id.Name), id.ThroughputSource), out var row)
                    ? ToEndpoint(row.Endpoint, row.LastCollectedDate)
                    : null))
                .ToList()
                .AsEnumerable();
        });

    public Task SaveEndpoint(Endpoint endpoint, CancellationToken cancellationToken) =>
        ExecuteWithDbContext(context =>
        {
            var normalizedName = Normalize(endpoint.Id.Name);
            var sanitizedName = endpoint.SanitizedName ?? string.Empty;
            var indicators = endpoint.EndpointIndicators?.ToList() ?? [];

            return context.UpsertAsync(
                [normalizedName, endpoint.Id.ThroughputSource],
                () => new LicensingEndpointEntity
                {
                    NormalizedName = normalizedName,
                    ThroughputSource = endpoint.Id.ThroughputSource,
                    Name = endpoint.Id.Name,
                    SanitizedName = sanitizedName,
                    NormalizedSanitizedName = Normalize(sanitizedName),
                    UserIndicator = endpoint.UserIndicator ?? string.Empty,
                    Scope = endpoint.Scope,
                    EndpointIndicators = indicators
                },
                row =>
                {
                    row.Name = endpoint.Id.Name;
                    row.SanitizedName = sanitizedName;
                    row.NormalizedSanitizedName = Normalize(sanitizedName);
                    row.UserIndicator = endpoint.UserIndicator ?? string.Empty;
                    row.Scope = endpoint.Scope;
                    row.EndpointIndicators = indicators;
                },
                cancellationToken);
        });

    public Task<IDictionary<string, IEnumerable<ThroughputData>>> GetEndpointThroughputByQueueName(IList<string> queueNames, CancellationToken cancellationToken) =>
        ExecuteWithDbContext(async context =>
        {
            var results = queueNames.ToDictionary(queueName => queueName, _ => Enumerable.Empty<ThroughputData>());

            var requestedByNormalizedName = new Dictionary<string, string>();
            foreach (var queueName in queueNames)
            {
                var normalizedName = Normalize(queueName);
                if (!requestedByNormalizedName.ContainsKey(normalizedName))
                {
                    requestedByNormalizedName[normalizedName] = queueName;
                }
            }

            var normalizedNames = requestedByNormalizedName.Keys.ToList();
            var from = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).AddMonths(-ReportedMonths);

            var rows = await context.LicensingEndpoints
                .AsNoTracking()
                .Where(endpoint => normalizedNames.Contains(endpoint.NormalizedSanitizedName))
                .Join(context.LicensingEndpointThroughput.Where(throughput => throughput.DateUtc >= from),
                    endpoint => new { endpoint.NormalizedName, endpoint.ThroughputSource },
                    throughput => new { throughput.NormalizedName, throughput.ThroughputSource },
                    (endpoint, throughput) => new
                    {
                        endpoint.NormalizedSanitizedName,
                        endpoint.ThroughputSource,
                        throughput.DateUtc,
                        throughput.MessageCount
                    })
                .ToListAsync(cancellationToken);

            foreach (var endpointRows in rows.GroupBy(row => (row.NormalizedSanitizedName, row.ThroughputSource)))
            {
                var throughputData = new ThroughputData(endpointRows
                    .OrderBy(row => row.DateUtc)
                    .Select(row => new EndpointDailyThroughput(row.DateUtc, row.MessageCount)))
                {
                    ThroughputSource = endpointRows.Key.ThroughputSource
                };

                var queueName = requestedByNormalizedName[endpointRows.Key.NormalizedSanitizedName];
                results[queueName] = results[queueName].Append(throughputData);
            }

            return (IDictionary<string, IEnumerable<ThroughputData>>)results;
        });

    public async Task RecordEndpointThroughput(string endpointName, ThroughputSource throughputSource, IList<EndpointDailyThroughput> throughput, CancellationToken cancellationToken)
    {
        if (throughput.Count == 0)
        {
            return;
        }

        // Only the first recording of a day can lose a race, and once its row exists every later
        // writer takes the update path, so a couple of attempts is enough.
        for (var attempt = 1; attempt <= MaxRecordAttempts; attempt++)
        {
            var recorded = await ExecuteWithDbContext(context =>
                TryRecordEndpointThroughput(context, endpointName, throughputSource, throughput, cancellationToken));

            if (recorded)
            {
                return;
            }
        }

        throw new InvalidOperationException(
            $"Could not record throughput for {endpointName} from {throughputSource} after {MaxRecordAttempts} attempts because of concurrent updates.");
    }

    static async Task<bool> TryRecordEndpointThroughput(ServiceControlDbContext context, string endpointName, ThroughputSource throughputSource, IList<EndpointDailyThroughput> throughput, CancellationToken cancellationToken)
    {
        var normalizedName = Normalize(endpointName);

        // Ordered so that concurrent calls covering overlapping days take the row locks in the same
        // order and cannot deadlock against each other.
        var dailyThroughput = throughput.OrderBy(entry => entry.DateUTC).ToList();

        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            var endpointExists = await context.LicensingEndpoints
                .AnyAsync(endpoint => endpoint.NormalizedName == normalizedName && endpoint.ThroughputSource == throughputSource, cancellationToken);

            if (!endpointExists)
            {
                throw new InvalidOperationException($"Endpoint {endpointName} from {throughputSource} does not exist ");
            }

            foreach (var (date, messageCount) in dailyThroughput)
            {
                // Recording adds to the day's total, because a source can report the same day
                // repeatedly. The addition happens in the database, so concurrent writers queue up on
                // the row instead of overwriting each other's totals.
                var updated = await context.LicensingEndpointThroughput
                    .Where(row => row.NormalizedName == normalizedName && row.ThroughputSource == throughputSource && row.DateUtc == date)
                    .ExecuteUpdateAsync(row => row.SetProperty(p => p.MessageCount, p => p.MessageCount + messageCount), cancellationToken);

                if (updated > 0)
                {
                    continue;
                }

                context.LicensingEndpointThroughput.Add(new LicensingEndpointThroughputEntity
                {
                    NormalizedName = normalizedName,
                    ThroughputSource = throughputSource,
                    DateUtc = date,
                    MessageCount = messageCount
                });

                try
                {
                    await context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException exception) when (context.IsDuplicateKeyException(exception))
                {
                    // Another writer created the day's row first, so this call has to add to it
                    // instead. The failed insert leaves the transaction unusable, hence the retry.
                    return false;
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }

    public Task UpdateUserIndicatorOnEndpoints(List<UpdateUserIndicator> userIndicatorUpdates, CancellationToken cancellationToken) =>
        ExecuteWithDbContext(async context =>
        {
            var updates = userIndicatorUpdates.ToDictionary(update => Normalize(update.Name), update => update.UserIndicator);
            var names = updates.Keys.ToList();

            var rows = await context.LicensingEndpoints
                .Where(endpoint => names.Contains(endpoint.NormalizedName) || names.Contains(endpoint.NormalizedSanitizedName))
                .ToListAsync(cancellationToken);

            var indicatorBySanitizedName = new Dictionary<string, string>();

            foreach (var row in rows)
            {
                if (updates.TryGetValue(row.NormalizedSanitizedName, out var valueFromSanitizedName))
                {
                    row.UserIndicator = valueFromSanitizedName;
                }
                else if (updates.TryGetValue(row.NormalizedName, out var valueFromName))
                {
                    row.UserIndicator = valueFromName;
                    indicatorBySanitizedName[row.NormalizedSanitizedName] = valueFromName;
                }
            }

            if (indicatorBySanitizedName.Count > 0)
            {
                var sanitizedNames = indicatorBySanitizedName.Keys.ToList();
                var alreadyLoaded = rows.Select(row => (row.NormalizedName, row.ThroughputSource)).ToHashSet();

                var siblings = await context.LicensingEndpoints
                    .Where(endpoint => sanitizedNames.Contains(endpoint.NormalizedSanitizedName))
                    .ToListAsync(cancellationToken);

                foreach (var sibling in siblings.Where(row => !alreadyLoaded.Contains((row.NormalizedName, row.ThroughputSource))))
                {
                    if (indicatorBySanitizedName.TryGetValue(sibling.NormalizedSanitizedName, out var indicator))
                    {
                        sibling.UserIndicator = indicator;
                    }
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        });

    public Task<bool> IsThereThroughputForLastXDays(int days, CancellationToken cancellationToken) =>
        IsThereThroughput(days, throughputSource: null, includeToday: false, cancellationToken);

    public Task<bool> IsThereThroughputForLastXDaysForSource(int days, ThroughputSource throughputSource, bool includeToday, CancellationToken cancellationToken) =>
        IsThereThroughput(days, throughputSource, includeToday, cancellationToken);

    Task<bool> IsThereThroughput(int days, ThroughputSource? throughputSource, bool includeToday, CancellationToken cancellationToken) =>
        ExecuteWithDbContext(context =>
        {
            var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
            var from = today.AddDays(-days);
            var to = includeToday ? today : today.AddDays(-1);

            var query = context.LicensingEndpointThroughput
                .AsNoTracking()
                .Where(row => row.DateUtc >= from && row.DateUtc <= to);

            if (throughputSource is not null)
            {
                query = query.Where(row => row.ThroughputSource == throughputSource);
            }

            return query.AnyAsync(cancellationToken);
        });

    public Task<BrokerMetadata> GetBrokerMetadata(CancellationToken cancellationToken) =>
        ExecuteWithDbContext(async context =>
            await context.GetSetting<BrokerMetadata>(SettingKeys.BrokerMetadata, cancellationToken) ?? DefaultBrokerMetadata);

    public Task SaveBrokerMetadata(BrokerMetadata brokerMetadata, CancellationToken cancellationToken) =>
        ExecuteWithDbContext(context => context.StoreSetting(SettingKeys.BrokerMetadata, brokerMetadata, cancellationToken));

    public Task<AuditServiceMetadata> GetAuditServiceMetadata(CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async context =>
            await context.GetSetting<AuditServiceMetadata>(SettingKeys.AuditServiceMetadata, cancellationToken) ?? DefaultAuditServiceMetadata);

    public Task SaveAuditServiceMetadata(AuditServiceMetadata auditServiceMetadata, CancellationToken cancellationToken) =>
        ExecuteWithDbContext(context => context.StoreSetting(SettingKeys.AuditServiceMetadata, auditServiceMetadata, cancellationToken));

    public Task<List<string>> GetReportMasks(CancellationToken cancellationToken) =>
        ExecuteWithDbContext(async context =>
            await context.GetSetting<List<string>>(SettingKeys.ReportMasks, cancellationToken) ?? []);

    public Task SaveReportMasks(List<string> reportMasks, CancellationToken cancellationToken) =>
        ExecuteWithDbContext(context => context.StoreSetting(SettingKeys.ReportMasks, reportMasks, cancellationToken));

    public Task<LicensedEndpointDetails?> GetLicensedEndpointDetails(CancellationToken cancellationToken) =>
        ExecuteWithDbContext(context => context.GetSetting<LicensedEndpointDetails>(SettingKeys.LicensedEndpointDetails, cancellationToken));

    public Task SaveLicensedEndpointDetails(LicensedEndpointDetails result, CancellationToken cancellationToken) =>
        ExecuteWithDbContext(context => context.StoreSetting(SettingKeys.LicensedEndpointDetails, result, cancellationToken));

    static Endpoint ToEndpoint(LicensingEndpointEntity row) => ToEndpoint(row, lastCollectedDate: null);

    static Endpoint ToEndpoint(LicensingEndpointEntity row, DateOnly? lastCollectedDate) =>
        new(new EndpointIdentifier(row.Name, row.ThroughputSource))
        {
            SanitizedName = row.SanitizedName,
            EndpointIndicators = [.. row.EndpointIndicators],
            UserIndicator = row.UserIndicator,
            Scope = row.Scope,
            LastCollectedDate = lastCollectedDate ?? default
        };

    static string Normalize(string name) => name.ToLowerInvariant();
}
