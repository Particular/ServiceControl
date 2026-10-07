namespace ServiceControl.Persistence.EFCore.DataMigration.Writers;

using DbContexts;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Writes the per-endpoint settings, and only for endpoints the target already knows. A setting for an unknown
/// endpoint is dropped by the heartbeat sync soon after the instance starts, so copying it would be work the
/// product undoes. That judgment holds because this category runs only once KnownEndpoints is Done or Abandoned,
/// so every endpoint that was going to be copied already has been. On a target whose name column ignores case,
/// two names that differ only in case are one row, and the writer reports each setting the insert drops as a merge.
/// </summary>
sealed class EndpointSettingsWriter(IMigrationSqlDialect migrationDialect) : IMigrationCategoryWriter
{
    public string CategoryId => MigrationCategoryIds.EndpointSettings;

    public int BatchSize(ServiceControlDbContext dbContext) => migrationDialect.RowsPerStatement(MigrationInsert<EndpointSettingsEntity>.For(dbContext).Columns.Count);

    public Task<long> Count(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default) =>
        dbContext.EndpointSettings.LongCountAsync(cancellationToken);

    public async Task<PreparedBatch> Prepare(ServiceControlDbContext dbContext, MigrationBatch batch, CancellationToken cancellationToken = default)
    {
        // Only the names this batch asks about: the whole table is a scan per batch, and a large instance
        // has as many settings as endpoints, so the cost grows with the square of the endpoint count.
        string[] batchNames = [.. batch.Rows.Select(row => ((EndpointSettings)row.Document).Name).Distinct(StringComparer.Ordinal)];

        // Ordinal, because HeartbeatEndpointSettingsSyncHostedService matches names in a default HashSet<string>.
        var knownNames = (await dbContext.KnownEndpoints.AsNoTracking()
            .Select(endpoint => endpoint.Name)
            .Where(name => batchNames.Contains(name))
            .ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);

        var rows = new List<EndpointSettingsEntity>(batch.Rows.Count);
        var rowSourceIds = new List<string>(batch.Rows.Count);
        var skips = new List<(string SourceId, MigrationSkipReason Reason, string Detail)>();

        foreach (var row in batch.Rows)
        {
            var settings = (EndpointSettings)row.Document;

            // The empty name is the row holding the default for every endpoint, which the sync keeps whatever endpoints are known.
            if (settings.Name != string.Empty && !knownNames.Contains(settings.Name))
            {
                skips.Add((row.SourceId, MigrationSkipReason.EndpointNotKnown, $"no known endpoint is named '{settings.Name}', so the heartbeat settings sync would delete this setting"));
                continue;
            }

            rows.Add(new EndpointSettingsEntity { Name = settings.Name, TrackInstances = settings.TrackInstances });
            rowSourceIds.Add(row.SourceId);
        }

        // The source yields document-id order, and the statement keeps the first of any keys the database treats as one.
        return new PreparedBatch(
            async (context, token) => (await migrationDialect.InsertMissing(context, rows, token)).Count,
            rows.Count,
            skips,
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
