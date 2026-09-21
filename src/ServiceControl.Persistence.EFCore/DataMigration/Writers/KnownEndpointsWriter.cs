namespace ServiceControl.Persistence.EFCore.DataMigration.Writers;

using DbContexts;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Writes the endpoints ServiceControl has heard from. The row's key is worked out from the endpoint's own
/// details rather than carried over, so the same endpoint lands on the same row whichever side wrote it first.
/// </summary>
sealed class KnownEndpointsWriter(IServiceScopeFactory scopeFactory, IMigrationSqlDialect migrationDialect) : IMigrationCategoryWriter
{
    public string CategoryId => MigrationCategoryIds.KnownEndpoints;

    // Opens a scope of its own, because BatchSize is handed no DbContext, unlike Prepare and Count.
    public int BatchSize
    {
        get
        {
            using var scope = scopeFactory.CreateScope();
            var entityType = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().Model.FindEntityType(typeof(KnownEndpointEntity))
                ?? throw new InvalidOperationException($"{nameof(KnownEndpointEntity)} is not an entity in the EF Core model.");

            return migrationDialect.RowsPerStatement(entityType.GetProperties().Count());
        }
    }

    public Task<long> Count(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default) =>
        dbContext.KnownEndpoints.LongCountAsync(cancellationToken);

    public Task<PreparedBatch> Prepare(ServiceControlDbContext dbContext, MigrationBatch batch, CancellationToken cancellationToken = default)
    {
        var rows = new List<KnownEndpointEntity>(batch.Rows.Count);
        var skips = new List<(string SourceId, MigrationSkipReason Reason, string Detail)>();

        foreach (var row in batch.Rows)
        {
            var endpoint = (KnownEndpoint)row.Document;

            if (endpoint.EndpointDetails?.Name is null || endpoint.EndpointDetails.Host is null)
            {
                var column = endpoint.EndpointDetails?.Name is null ? nameof(KnownEndpointEntity.Name) : nameof(KnownEndpointEntity.Host);
                skips.Add((row.SourceId, MigrationSkipReason.RequiredValueMissing, $"KnownEndpoints.{column} is NOT NULL and the document has no value for it"));
                continue;
            }

            rows.Add(new KnownEndpointEntity
            {
                Id = endpoint.EndpointDetails.GetDeterministicId(),
                Name = endpoint.EndpointDetails.Name,
                HostId = endpoint.EndpointDetails.HostId,
                Host = endpoint.EndpointDetails.Host,
                Monitored = endpoint.Monitored
            });
        }

        return Task.FromResult(new PreparedBatch(
            async (context, token) => (await migrationDialect.InsertMissing(context, rows, token)).Count,
            rows.Count,
            skips));
    }
}
