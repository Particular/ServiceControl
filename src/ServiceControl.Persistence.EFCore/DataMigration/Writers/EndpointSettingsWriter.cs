namespace ServiceControl.Persistence.EFCore.DataMigration.Writers;

using DbContexts;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Writes the per-endpoint settings, and only for endpoints the target already knows. A setting for an unknown
/// endpoint is dropped by the heartbeat sync soon after the instance starts, so copying it would be work the
/// product undoes. That judgment holds only while the endpoints themselves copied cleanly, which is why this
/// writer reads the KnownEndpoints checkpoint before it decides a skip was no loss.
/// </summary>
sealed class EndpointSettingsWriter(IServiceScopeFactory scopeFactory, IMigrationSqlDialect migrationDialect) : IMigrationCategoryWriter
{
    public string CategoryId => MigrationCategoryIds.EndpointSettings;

    // Opens a scope of its own, because BatchSize is handed no DbContext, unlike Prepare and Count.
    public int BatchSize
    {
        get
        {
            using var scope = scopeFactory.CreateScope();
            var entityType = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().Model.FindEntityType(typeof(EndpointSettingsEntity))
                ?? throw new InvalidOperationException($"{nameof(EndpointSettingsEntity)} is not an entity in the EF Core model.");

            return migrationDialect.RowsPerStatement(entityType.GetProperties().Count());
        }
    }

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

        // The names come from the target's own table, so an endpoint the KnownEndpoints copy dropped looks the same here as one the source never had.
        var knownEndpointsSkipped = await dbContext.MigrationCheckpoints
            .AsNoTracking()
            .Where(checkpoint => checkpoint.CategoryId == MigrationCategoryIds.KnownEndpoints)
            .Select(checkpoint => checkpoint.SkippedCount)
            .SingleOrDefaultAsync(cancellationToken);

        var rows = new List<EndpointSettingsEntity>(batch.Rows.Count);
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
        }

        // The source yields document-id order, and the statement keeps the first of any keys the database treats as one.
        return new PreparedBatch(
            async (context, token) => (await migrationDialect.InsertMissing(context, rows, token)).Count,
            rows.Count,
            skips,
            SkipsReflectTheSource: knownEndpointsSkipped == 0);
    }
}
