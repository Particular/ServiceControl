namespace ServiceControl.Persistence.EFCore.DataMigration.Writers;

using DbContexts;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Particular.LicensingComponent.Contracts;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Writes the licensing endpoints. An endpoint the target already holds, or a second one in the same batch whose key
/// the column treats as the same, is left as it is and counted as already present.
/// </summary>
sealed class LicensingEndpointsWriter(IMigrationSqlDialect migrationDialect) : MigrationCategoryWriter<Endpoint>
{
    public override string CategoryId => MigrationCategoryIds.LicensingEndpoints;

    public override int BatchSize(ServiceControlDbContext dbContext) => migrationDialect.RowsPerStatementFor<LicensingEndpointEntity>(dbContext);

    public override Task<long> Count(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default) =>
        dbContext.LicensingEndpoints.LongCountAsync(cancellationToken);

    protected override Task<PreparedBatch> PrepareDocuments(ServiceControlDbContext dbContext, IReadOnlyList<(MigrationRow Row, Endpoint Document)> documents, CancellationToken cancellationToken = default)
    {
        var insert = MigrationInsert<LicensingEndpointEntity>.For(dbContext);
        var rows = new List<LicensingEndpointEntity>(documents.Count);
        var skips = new List<(string SourceId, MigrationSkipReason Reason, string Detail)>();

        foreach (var (row, endpoint) in documents.OrderBy(document => document.Row.SourceId, StringComparer.Ordinal))
        {
            if (endpoint.Id.Name is null)
            {
                skips.Add((row.SourceId, MigrationSkipReason.RequiredValueMissing, "LicensingEndpoints.Name and NormalizedName are NOT NULL and the document has no endpoint name"));
                continue;
            }

            var entity = LicensingEndpointRowMapper.Map(endpoint);

            if (UnstorableValueSkip(insert, migrationDialect, row, entity) is { } unstorable)
            {
                skips.Add(unstorable);
                continue;
            }

            rows.Add(entity);
        }

        return Task.FromResult(new PreparedBatch(
            async (context, token) => (await migrationDialect.InsertMissing(context, rows, token)).Count,
            rows.Count,
            skips,
            Merges: []));
    }
}
