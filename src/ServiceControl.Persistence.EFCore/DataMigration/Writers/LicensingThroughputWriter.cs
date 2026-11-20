namespace ServiceControl.Persistence.EFCore.DataMigration.Writers;

using DbContexts;
using Entities;
using Implementation;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Particular.LicensingComponent.Contracts;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Adds each day's message count to the stored count for that day, and the database sums endpoints whose names its key treats as one.
/// A day repeated for one source endpoint keeps its last row, and a retry deletes every copied day first.
/// It reports no merge, because the endpoints writer reports an endpoint the key folds into another.
/// Throws <see cref="InvalidOperationException" /> when a batch names an endpoint the target does not hold.
/// </summary>
sealed class LicensingThroughputWriter(IMigrationSqlDialect migrationDialect) : MigrationCategoryWriter<ThroughputDayRow>, IStoreKeyedCategoryWriter
{
    public override string CategoryId => MigrationCategoryIds.LicensingThroughput;

    public override int BatchSize(ServiceControlDbContext dbContext) => migrationDialect.RowsPerStatementFor<LicensingEndpointThroughputEntity>(dbContext);

    public override Task<long> Count(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default) =>
        dbContext.LicensingEndpointThroughput.LongCountAsync(cancellationToken);

    public Task<int> DeleteCopiedRows(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default) =>
        dbContext.LicensingEndpointThroughput.ExecuteDeleteAsync(cancellationToken);

    protected override async Task<PreparedBatch> PrepareDocuments(ServiceControlDbContext dbContext, IReadOnlyList<(MigrationRow Row, ThroughputDayRow Document)> documents, CancellationToken cancellationToken = default)
    {
        var insert = MigrationInsert<LicensingEndpointThroughputEntity>.For(dbContext);
        var mapped = new List<(EndpointIdentifier Endpoint, LicensingEndpointThroughputEntity Day)>(documents.Count);
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

            mapped.Add((day.Endpoint, entity));
        }

        foreach (var endpoint in mapped.Select(day => day.Endpoint).Distinct())
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
                .GroupBy(day => (day.Endpoint.Name, day.Endpoint.ThroughputSource, day.Day.DateUtc))
                .Select(sameEndpointDay => sameEndpointDay.Last().Day)
        ];

        // Adding is safe to resume, because the batch and its checkpoint commit in one transaction, and a replayed checkpoint save is refused.
        return new PreparedBatch((context, token) => migrationDialect.AddLicensingEndpointThroughput(context, rows, token), mapped.Count, skips, Merges: []);
    }
}
