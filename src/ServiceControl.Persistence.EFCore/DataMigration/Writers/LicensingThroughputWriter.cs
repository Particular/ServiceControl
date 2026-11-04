namespace ServiceControl.Persistence.EFCore.DataMigration.Writers;

using DbContexts;
using Entities;
using Implementation;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Particular.LicensingComponent.Contracts;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Writes the licensing endpoints' daily message counts, setting each day's count rather than adding to it, so a copy
/// run twice leaves the same numbers. A batch naming an endpoint the target does not hold is refused with an
/// <see cref="InvalidOperationException" /> naming that endpoint and its source. A day whose key repeats in the batch
/// keeps its last row, and the others are counted as already present.
/// </summary>
sealed class LicensingThroughputWriter(IMigrationSqlDialect migrationDialect) : MigrationCategoryWriter<ThroughputDayRow>
{
    public override string CategoryId => MigrationCategoryIds.LicensingThroughput;

    public override int BatchSize(ServiceControlDbContext dbContext) => migrationDialect.RowsPerStatementFor<LicensingEndpointThroughputEntity>(dbContext);

    public override Task<long> Count(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default) =>
        dbContext.LicensingEndpointThroughput.LongCountAsync(cancellationToken);

    protected override async Task<PreparedBatch> PrepareDocuments(ServiceControlDbContext dbContext, IReadOnlyList<(MigrationRow Row, ThroughputDayRow Document)> documents, CancellationToken cancellationToken = default)
    {
        var insert = MigrationInsert<LicensingEndpointThroughputEntity>.For(dbContext);
        var nameComparer = migrationDialect.KeyComparer(typeof(LicensingEndpointThroughputEntity), nameof(LicensingEndpointThroughputEntity.NormalizedName));
        var mapped = new List<LicensingEndpointThroughputEntity>(documents.Count);
        var endpoints = new List<EndpointIdentifier>(documents.Count);
        var skips = new List<(string SourceId, MigrationSkipReason Reason, string Detail)>();

        // Date order is the order the product's own throughput write takes row locks in.
        foreach (var (row, day) in documents.OrderBy(document => document.Document.DateUtc).ThenBy(document => document.Row.SourceId, StringComparer.Ordinal))
        {
            if (day.Endpoint.Name is null)
            {
                skips.Add((row.SourceId, MigrationSkipReason.RequiredValueMissing, "LicensingEndpointThroughput.NormalizedName is NOT NULL and the day's endpoint has no name"));
                continue;
            }

            var entity = LicensingThroughputRowMapper.Map(day);

            if (UnstorableValueSkip(insert, migrationDialect, row, entity) is { } unstorable)
            {
                skips.Add(unstorable);
                continue;
            }

            mapped.Add(entity);
            endpoints.Add(day.Endpoint);
        }

        foreach (var endpoint in endpoints.Distinct())
        {
            var normalizedName = LicensingDataStore.Normalize(endpoint.Name);

            // Checked in SQL rather than in memory, so the name compares under the column's collation as the foreign key does.
            if (!await dbContext.LicensingEndpoints.AnyAsync(row => row.NormalizedName == normalizedName && row.ThroughputSource == endpoint.ThroughputSource, cancellationToken))
            {
                throw new InvalidOperationException($"Endpoint {endpoint.Name} from {endpoint.ThroughputSource} does not exist in the migration target, so its daily throughput cannot be copied. LicensingThroughput copies only after LicensingEndpoints has finished.");
            }
        }

        List<LicensingEndpointThroughputEntity> rows =
        [
            .. mapped
                .GroupBy(day => (day.ThroughputSource, day.DateUtc))
                .SelectMany(sameDay => sameDay.GroupBy(day => day.NormalizedName, nameComparer).Select(sameKey => sameKey.Last()))
        ];

        return new PreparedBatch((context, token) => migrationDialect.SetLicensingEndpointThroughput(context, rows, token), mapped.Count, skips, Merges: []);
    }
}
