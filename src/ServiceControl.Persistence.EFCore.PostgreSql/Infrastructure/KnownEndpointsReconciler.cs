namespace ServiceControl.Persistence.EFCore.PostgreSql.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

class KnownEndpointsReconciler(
    ILogger<KnownEndpointsReconciler> logger,
    TimeProvider timeProvider,
    IServiceScopeFactory serviceScopeFactory)
    : InsertOnlyTableReconciler<KnownEndpointInsertOnlyEntity, KnownEndpointEntity>(
        logger, timeProvider, serviceScopeFactory, nameof(KnownEndpointsReconciler))
{
    protected override Task<int> ReconcileBatch(ServiceControlDbContext dbContext, CancellationToken stoppingToken) =>
        ReconcileBatch(dbContext, BatchSize, stoppingToken);

    // Static so tests can execute a batch deterministically without the background service's timer loop.
    // Must be called within an active transaction because of the pg_try_advisory_xact_lock.
    internal static async Task<int> ReconcileBatch(ServiceControlDbContext dbContext, int batchSize, CancellationToken cancellationToken)
    {
        var sql = """
            WITH lock_check AS (
                SELECT pg_try_advisory_xact_lock(hashtext('known_endpoints_sync')) AS acquired
            ),
            batch AS (
                SELECT ctid FROM "known_endpoints_insert_only"
                WHERE (SELECT acquired FROM lock_check)
                LIMIT @batchSize
            ),
            ins AS (
                INSERT INTO "known_endpoints" ("id", "name", "host_id", "host", "monitored")
                SELECT DISTINCT ON ("known_endpoint_id") "known_endpoint_id", "name", "host_id", "host", FALSE
                FROM "known_endpoints_insert_only"
                WHERE ctid IN (SELECT ctid FROM batch)
                ON CONFLICT ("id") DO NOTHING
            )
            DELETE FROM "known_endpoints_insert_only"
            WHERE ctid IN (SELECT ctid FROM batch);
            """;

        var rowsAffected = await dbContext.Database.ExecuteSqlRawAsync(sql, [new Npgsql.NpgsqlParameter("@batchSize", batchSize)], cancellationToken);
        return rowsAffected;
    }
}
