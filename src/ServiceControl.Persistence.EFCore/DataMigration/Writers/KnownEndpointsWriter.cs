namespace ServiceControl.Persistence.EFCore.DataMigration.Writers;

using DbContexts;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Writes the endpoints ServiceControl has heard from. The row's key is worked out from the endpoint's own
/// details rather than carried over, so the same endpoint lands on the same row whichever side wrote it first.
/// </summary>
sealed class KnownEndpointsWriter(IMigrationSqlDialect migrationDialect) : MigrationCategoryWriter<KnownEndpoint>
{
    public override string CategoryId => MigrationCategoryIds.KnownEndpoints;

    public override int BatchSize(ServiceControlDbContext dbContext) => migrationDialect.RowsPerStatement(MigrationInsert<KnownEndpointEntity>.For(dbContext).Columns.Count);

    public override Task<long> Count(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default) =>
        dbContext.KnownEndpoints.LongCountAsync(cancellationToken);

    protected override Task<PreparedBatch> PrepareDocuments(ServiceControlDbContext dbContext, IReadOnlyList<(MigrationRow Row, KnownEndpoint Document)> documents, CancellationToken cancellationToken = default)
    {
        var rows = new List<KnownEndpointEntity>(documents.Count);
        var skips = new List<(string SourceId, MigrationSkipReason Reason, string Detail)>();

        foreach (var (row, endpoint) in documents)
        {
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
            skips,
            Merges: []));
    }
}
