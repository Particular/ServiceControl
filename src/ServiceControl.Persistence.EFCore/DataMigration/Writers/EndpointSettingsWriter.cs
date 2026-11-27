namespace ServiceControl.Persistence.EFCore.DataMigration.Writers;

using DbContexts;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Copies every per-endpoint setting as stored.
/// Where the name column's collation ignores case, accents, kana type or width, names that differ only in those become one row, and the dropped ones are
/// reported as merges. The row kept is the one whose name matches a known endpoint exactly.
/// </summary>
sealed class EndpointSettingsWriter(IMigrationSqlDialect migrationDialect) : MigrationCategoryWriter<EndpointSettings>
{
    public override string CategoryId => MigrationCategoryIds.EndpointSettings;

    public override int BatchSize(ServiceControlDbContext dbContext) => migrationDialect.RowsPerStatementFor<EndpointSettingsEntity>(dbContext);

    public override Task<long> Count(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default) =>
        dbContext.EndpointSettings.LongCountAsync(cancellationToken);

    protected override async Task<PreparedBatch> PrepareDocuments(ServiceControlDbContext dbContext, IReadOnlyList<(MigrationRow Row, EndpointSettings Document)> documents, CancellationToken cancellationToken = default)
    {
        var insert = MigrationInsert<EndpointSettingsEntity>.For(dbContext);
        List<(string SourceId, EndpointSettingsEntity Row)> rows = [];
        List<(string SourceId, MigrationSkipReason Reason, string Detail)> skips = [];

        foreach (var (row, settings) in documents)
        {
            var entity = new EndpointSettingsEntity { Name = settings.Name, TrackInstances = settings.TrackInstances };

            if (UnstorableValueSkip(insert, migrationDialect, row, entity) is { } unstorable)
            {
                skips.Add(unstorable);
                continue;
            }

            rows.Add((row.SourceId, entity));
        }

        rows = await KnownEndpointNamesFirst(dbContext, rows, cancellationToken);

        // The insert keeps the first of any names the database treats as equal.
        return new PreparedBatch(
            async (context, token) => (await migrationDialect.InsertMissing(context, [.. rows.Select(row => row.Row)], token)).Count,
            rows.Count,
            skips,
            await KeyMerges.In(dbContext, migrationDialect, rows, cancellationToken));
    }

    async Task<List<(string SourceId, EndpointSettingsEntity Row)>> KnownEndpointNamesFirst(ServiceControlDbContext dbContext, List<(string SourceId, EndpointSettingsEntity Row)> rows, CancellationToken cancellationToken)
    {
        var nameComparer = migrationDialect.KeyComparer(typeof(EndpointSettingsEntity), nameof(EndpointSettingsEntity.Name));
        string[] mergingNames = [.. rows.GroupBy(row => row.Row.Name, nameComparer).Where(group => group.Count() > 1).SelectMany(group => group.Select(row => row.Row.Name))];

        if (mergingNames.Length == 0)
        {
            return rows;
        }

        var knownNames = (await dbContext.KnownEndpoints.Where(endpoint => mergingNames.Contains(endpoint.Name)).Select(endpoint => endpoint.Name).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);

        // OrderBy is stable, so only the known names move to the front.
        return [.. rows.OrderBy(row => knownNames.Contains(row.Row.Name) ? 0 : 1)];
    }
}
