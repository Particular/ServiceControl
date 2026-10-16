namespace ServiceControl.Persistence.EFCore.DataMigration.Writers;

using DbContexts;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Writes every per-endpoint setting as stored. A setting for an endpoint the target does not know is copied too,
/// and the heartbeat settings sync removes it by its own rule once the instance opens. On a target whose name
/// column ignores case, two names that differ only in case are one row, and the writer reports each setting the
/// insert drops as a merge.
/// </summary>
sealed class EndpointSettingsWriter(IMigrationSqlDialect migrationDialect) : MigrationCategoryWriter<EndpointSettings>
{
    public override string CategoryId => MigrationCategoryIds.EndpointSettings;

    public override int BatchSize(ServiceControlDbContext dbContext) => migrationDialect.RowsPerStatement(MigrationInsert<EndpointSettingsEntity>.For(dbContext).Columns.Count);

    public override Task<long> Count(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default) =>
        dbContext.EndpointSettings.LongCountAsync(cancellationToken);

    protected override async Task<PreparedBatch> PrepareDocuments(ServiceControlDbContext dbContext, IReadOnlyList<(MigrationRow Row, EndpointSettings Document)> documents, CancellationToken cancellationToken = default)
    {
        List<EndpointSettingsEntity> rows = [.. documents.Select(document => new EndpointSettingsEntity { Name = document.Document.Name, TrackInstances = document.Document.TrackInstances })];
        List<string> rowSourceIds = [.. documents.Select(document => document.Row.SourceId)];

        // The source yields document-id order, and the statement keeps the first of any keys the database treats as one.
        return new PreparedBatch(
            async (context, token) => (await migrationDialect.InsertMissing(context, rows, token)).Count,
            rows.Count,
            [],
            await MergesIn(dbContext, rows, rowSourceIds, cancellationToken));
    }

    // Mirrors the insert's de-duplication, so an operator can see which settings a case-insensitive name column dropped.
    async Task<IReadOnlyList<(string SourceId, string Detail)>> MergesIn(ServiceControlDbContext dbContext, List<EndpointSettingsEntity> rows, List<string> rowSourceIds, CancellationToken cancellationToken)
    {
        var sameName = migrationDialect.KeyComparer(typeof(EndpointSettingsEntity), nameof(EndpointSettingsEntity.Name));
        string[] rowNames = [.. rows.Select(row => row.Name)];

        // The database compares under the column's collation, so this also returns a stored name that differs only in case.
        var keptNames = await dbContext.EndpointSettings.AsNoTracking()
            .Select(stored => stored.Name)
            .Where(name => rowNames.Contains(name))
            .ToListAsync(cancellationToken);

        var merges = new List<(string SourceId, string Detail)>();

        for (var index = 0; index < rows.Count; index++)
        {
            var name = rows[index].Name;
            var keptName = keptNames.FirstOrDefault(kept => sameName.Equals(kept, name));

            if (keptName is null)
            {
                keptNames.Add(name);
            }
            else if (!string.Equals(keptName, name, StringComparison.Ordinal))
            {
                merges.Add((rowSourceIds[index], $"the name column treats '{name}' and '{keptName}' as one endpoint, so it kept '{keptName}' and dropped the settings of '{name}'"));
            }
        }

        return merges;
    }
}
