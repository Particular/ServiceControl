namespace ServiceControl.Persistence.EFCore.DataMigration.Writers;

using DbContexts;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using ServiceControl.Contracts.CustomChecks;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Writes the custom checks under the id the product derives from the endpoint and the check, and never replaces one the target already holds.
/// A row with SourceStorageCheck true is skipped as SourceStorageCheck, which is harmless, and a SourceStorageCheck value that is not a bool throws.
/// A row with a required text value left null is skipped as RequiredValueMissing, and one with a character the database refuses as MalformedValue.
/// </summary>
sealed class CustomChecksWriter(IMigrationSqlDialect migrationDialect) : MigrationCategoryWriter<CustomCheckDetail>
{
    public override string CategoryId => MigrationCategoryIds.CustomChecks;

    public override int BatchSize(ServiceControlDbContext dbContext) => migrationDialect.RowsPerStatementFor<CustomCheckEntity>(dbContext);

    public override Task<long> Count(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default) =>
        dbContext.CustomChecks.LongCountAsync(cancellationToken);

    protected override Task<PreparedBatch> PrepareDocuments(ServiceControlDbContext dbContext, IReadOnlyList<(MigrationRow Row, CustomCheckDetail Document)> documents, CancellationToken cancellationToken = default)
    {
        var insert = MigrationInsert<CustomCheckEntity>.For(dbContext);
        List<CustomCheckEntity> rows = [];
        List<(string SourceId, MigrationSkipReason Reason, string Detail)> skips = [];

        foreach (var (row, detail) in documents)
        {
            if (IsSourceStorageCheck(row))
            {
                skips.Add((row.SourceId, MigrationSkipReason.SourceStorageCheck, $"'{detail.CustomCheckId}' under '{detail.Category}' is a check the source's own storage runs about itself, which nothing reports once the instance runs on the target"));
                continue;
            }

            var column = detail.CustomCheckId is null ? nameof(CustomCheckEntity.CustomCheckId)
                : detail.Category is null ? nameof(CustomCheckEntity.Category)
                : detail.OriginatingEndpoint?.Name is null ? nameof(CustomCheckEntity.OriginatingEndpointName)
                : detail.OriginatingEndpoint.Host is null ? nameof(CustomCheckEntity.OriginatingEndpointHost)
                : null;

            if (column is not null)
            {
                skips.Add((row.SourceId, MigrationSkipReason.RequiredValueMissing, $"CustomChecks.{column} is NOT NULL and the document has no value for it"));
                continue;
            }

            var entity = CustomCheckRowMapper.Map(detail);

            if (UnstorableValueSkip(insert, migrationDialect, row, entity) is { } unstorable)
            {
                skips.Add(unstorable);
                continue;
            }

            rows.Add(entity);
        }

        return Task.FromResult(new PreparedBatch(async (context, token) => (await migrationDialect.InsertMissing(context, rows, token)).Count, rows.Count, skips, Merges: []));
    }

    static bool IsSourceStorageCheck(MigrationRow row) =>
        row.Metadata.GetValueOrDefault(MigrationMetadataKeys.SourceStorageCheck) switch
        {
            bool flagged => flagged,
            null => false,
            var other => throw new InvalidOperationException($"Row '{row.SourceId}' carries a {other.GetType().FullName} as its {MigrationMetadataKeys.SourceStorageCheck} value, where the source sets a {typeof(bool).FullName}")
        };
}
