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
    const int MaxRecordAttempts = 5;

    static readonly string PlatformEndpointIndicator = EndpointIndicator.PlatformEndpoint.ToString();
    static readonly AuditServiceMetadata DefaultAuditServiceMetadata = new([], []);
    static readonly BrokerMetadata DefaultBrokerMetadata = new(null, []);

    public Task<IEnumerable<Endpoint>> GetAllEndpoints(bool includePlatformEndpoints, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (context, token) =>
        {
            var query = context.LicensingEndpoints.AsNoTracking();

            if (!includePlatformEndpoints)
            {
                query = query.Where(endpoint => !endpoint.EndpointIndicators.Contains(PlatformEndpointIndicator));
            }

            var rows = await query.ToListAsync(token);

            return rows.Select(ToEndpoint);
        }, cancellationToken);

    public Task<Endpoint?> GetEndpoint(EndpointIdentifier id, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (context, token) =>
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
                .SingleOrDefaultAsync(token);

            return row is null ? null : ToEndpoint(row.Endpoint, row.LastCollectedDate);
        }, cancellationToken);

    public Task<IEnumerable<(EndpointIdentifier Id, Endpoint? Endpoint)>> GetEndpoints(IList<EndpointIdentifier> endpointIds, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (context, token) =>
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
                .ToListAsync(token);

            var found = rows.ToDictionary(row => (row.Endpoint.NormalizedName, row.Endpoint.ThroughputSource));

            return endpointIds
                .Select(id => (id, found.TryGetValue((Normalize(id.Name), id.ThroughputSource), out var row)
                    ? ToEndpoint(row.Endpoint, row.LastCollectedDate)
                    : null))
                .ToList()
                .AsEnumerable();
        }, cancellationToken);

    public Task SaveEndpoint(Endpoint endpoint, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext((context, token) =>
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
                token);
        }, cancellationToken);

    public Task<IDictionary<string, IEnumerable<ThroughputData>>> GetEndpointThroughputByQueueName(IList<string> queueNames, DateOnly? throughputMaxDate = null, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (context, token) =>
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
            var from = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).AddMonths(-ThroughputReporting.ReportedMonths);

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
                .ToListAsync(token);

            foreach (var endpointRows in rows.GroupBy(row => (row.NormalizedSanitizedName, row.ThroughputSource)))
            {
                var throughputData = new ThroughputData(endpointRows
                    .Where(row => !throughputMaxDate.HasValue || row.DateUtc < throughputMaxDate.Value)
                    .OrderBy(row => row.DateUtc)
                    .Select(row => new EndpointDailyThroughput(row.DateUtc, row.MessageCount)))
                {
                    ThroughputSource = endpointRows.Key.ThroughputSource
                };

                var queueName = requestedByNormalizedName[endpointRows.Key.NormalizedSanitizedName];
                results[queueName] = results[queueName].Append(throughputData);
            }

            return (IDictionary<string, IEnumerable<ThroughputData>>)results;
        }, cancellationToken);

    public async Task RecordEndpointThroughput(string endpointName, ThroughputSource throughputSource, IList<EndpointDailyThroughput> throughput, CancellationToken cancellationToken = default)
    {
        if (throughput.Count == 0)
        {
            return;
        }

        // Only the first recording of a day can lose a race, and once its row exists every later
        // writer takes the update path, so a couple of attempts is enough.
        for (var attempt = 1; attempt <= MaxRecordAttempts; attempt++)
        {
            var recorded = await ExecuteWithDbContext((context, token) =>
                TryRecordEndpointThroughput(context, endpointName, throughputSource, throughput, token), cancellationToken);

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

    public Task RemoveEndpoints(EndpointIdentifier[] endpointIds, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (context, token) =>
        {
            foreach (var endpointsBySource in endpointIds.GroupBy(endpoint => endpoint.ThroughputSource))
            {
                var normalizedNames = endpointsBySource
                    .Select(endpoint => Normalize(endpoint.Name))
                    .Distinct()
                    .ToList();

                await context.LicensingEndpoints
                    .Where(endpoint => endpoint.ThroughputSource == endpointsBySource.Key && normalizedNames.Contains(endpoint.NormalizedName))
                    .ExecuteDeleteAsync(token);
            }
        }, cancellationToken);

    public Task UpdateUserIndicatorOnEndpoints(List<UpdateUserIndicator> userIndicatorUpdates, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (context, token) =>
        {
            var updates = userIndicatorUpdates.ToDictionary(update => Normalize(update.Name), update => update.UserIndicator);
            var names = updates.Keys.ToList();

            var rows = await context.LicensingEndpoints
                .Where(endpoint => names.Contains(endpoint.NormalizedName) || names.Contains(endpoint.NormalizedSanitizedName))
                .ToListAsync(token);

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
                    .ToListAsync(token);

                foreach (var sibling in siblings.Where(row => !alreadyLoaded.Contains((row.NormalizedName, row.ThroughputSource))))
                {
                    if (indicatorBySanitizedName.TryGetValue(sibling.NormalizedSanitizedName, out var indicator))
                    {
                        sibling.UserIndicator = indicator;
                    }
                }
            }

            await context.SaveChangesAsync(token);
        }, cancellationToken);

    public Task<bool> IsThereThroughputForLastXDays(int days, CancellationToken cancellationToken = default) =>
        IsThereThroughput(days, throughputSource: null, includeToday: false, cancellationToken);

    public Task<bool> IsThereThroughputForLastXDaysForSource(int days, ThroughputSource throughputSource, bool includeToday, CancellationToken cancellationToken = default) =>
        IsThereThroughput(days, throughputSource, includeToday, cancellationToken);

    Task<bool> IsThereThroughput(int days, ThroughputSource? throughputSource, bool includeToday, CancellationToken cancellationToken) =>
        ExecuteWithDbContext((context, token) =>
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

            return query.AnyAsync(token);
        }, cancellationToken);

    public Task<BrokerMetadata> GetBrokerMetadata(CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (context, token) =>
            await context.GetSetting<BrokerMetadata>(SettingKeys.BrokerMetadata, token) ?? DefaultBrokerMetadata, cancellationToken);

    public Task SaveBrokerMetadata(BrokerMetadata brokerMetadata, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext((context, token) => context.StoreSetting(SettingKeys.BrokerMetadata, brokerMetadata, token), cancellationToken);

    public Task<AuditServiceMetadata> GetAuditServiceMetadata(CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (context, token) =>
            await context.GetSetting<AuditServiceMetadata>(SettingKeys.AuditServiceMetadata, token) ?? DefaultAuditServiceMetadata, cancellationToken);

    public Task SaveAuditServiceMetadata(AuditServiceMetadata auditServiceMetadata, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext((context, token) => context.StoreSetting(SettingKeys.AuditServiceMetadata, auditServiceMetadata, token), cancellationToken);

    public Task<List<string>> GetReportMasks(CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (context, token) =>
            await context.GetSetting<List<string>>(SettingKeys.ReportMasks, token) ?? [], cancellationToken);

    public Task SaveReportMasks(List<string> reportMasks, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext((context, token) => context.StoreSetting(SettingKeys.ReportMasks, reportMasks, token), cancellationToken);

    public Task<LicensedEndpointDetails?> GetLicensedEndpointDetails(CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext((context, token) => context.GetSetting<LicensedEndpointDetails>(SettingKeys.LicensedEndpointDetails, token), cancellationToken);

    public Task SaveLicensedEndpointDetails(LicensedEndpointDetails result, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext((context, token) => context.StoreSetting(SettingKeys.LicensedEndpointDetails, result, token), cancellationToken);

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
