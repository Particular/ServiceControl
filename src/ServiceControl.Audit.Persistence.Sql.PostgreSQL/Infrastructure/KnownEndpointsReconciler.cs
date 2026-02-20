namespace ServiceControl.Audit.Persistence.Sql.PostgreSQL.Infrastructure;

using Core.DbContexts;
using Core.Entities;
using Core.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

class KnownEndpointsReconciler(
    ILogger<KnownEndpointsReconciler> logger,
    TimeProvider timeProvider,
    IServiceScopeFactory serviceScopeFactory)
    : InsertOnlyTableReconciler<KnownEndpointInsertOnlyEntity, KnownEndpointEntity>(
        logger, timeProvider, serviceScopeFactory, nameof(KnownEndpointsReconciler))
{
    protected override async Task<int> ReconcileBatch(AuditDbContextBase dbContext, CancellationToken stoppingToken)
    {
        var sql = @"
            WITH lock_check AS (
                SELECT pg_try_advisory_xact_lock(hashtext('known_endpoints_sync')) AS acquired
            ),
            deleted AS (
                DELETE FROM known_endpoints_insert_only
                WHERE (SELECT acquired FROM lock_check)
                  AND ctid IN (
                    SELECT ctid FROM known_endpoints_insert_only LIMIT @batchSize
                  )
                RETURNING known_endpoint_id, name, host_id, host, last_seen
            ),
            aggregated AS (
                SELECT DISTINCT ON (known_endpoint_id) known_endpoint_id, name, host_id, host, last_seen
                FROM deleted
                ORDER BY known_endpoint_id, last_seen DESC
            )
            INSERT INTO known_endpoints (id, name, host_id, host, last_seen)
            SELECT known_endpoint_id, name, host_id, host, last_seen
            FROM aggregated
            ON CONFLICT (id) DO UPDATE SET
                last_seen = GREATEST(known_endpoints.last_seen, EXCLUDED.last_seen);
        ";

        var rowsAffected = await dbContext.Database.ExecuteSqlRawAsync(sql, [new Npgsql.NpgsqlParameter("@batchSize", BatchSize)], stoppingToken);
        return rowsAffected;
    }
}
