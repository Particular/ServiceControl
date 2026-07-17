namespace ServiceControl.Persistence.EFCore.SqlServer.Infrastructure;

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
    // Must be called within an active transaction because of the sp_getapplock Transaction lock owner.
    internal static async Task<int> ReconcileBatch(ServiceControlDbContext dbContext, int batchSize, CancellationToken cancellationToken)
    {
        var sql = """
            DECLARE @lockResult INT;
            EXEC @lockResult = sp_getapplock @Resource = 'known_endpoints_sync', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 0;
            IF @lockResult < 0
            BEGIN
                SELECT 0;
                RETURN;
            END;

            DECLARE @deleted TABLE (
                KnownEndpointId UNIQUEIDENTIFIER,
                Name NVARCHAR(MAX),
                HostId UNIQUEIDENTIFIER,
                Host NVARCHAR(MAX)
            );

            DELETE TOP (@batchSize) FROM KnownEndpointsInsertOnly
            OUTPUT DELETED.KnownEndpointId, DELETED.Name, DELETED.HostId, DELETED.Host
            INTO @deleted;

            WITH ranked AS (
                SELECT KnownEndpointId, Name, HostId, Host,
                       ROW_NUMBER() OVER (PARTITION BY KnownEndpointId ORDER BY (SELECT NULL)) AS rn
                FROM @deleted
            ),
            aggregated AS (
                SELECT KnownEndpointId, Name, HostId, Host
                FROM ranked
                WHERE rn = 1
            )
            MERGE INTO KnownEndpoints AS target
            USING aggregated AS source
            ON target.Id = source.KnownEndpointId
            WHEN NOT MATCHED THEN
                INSERT (Id, Name, HostId, Host, Monitored)
                VALUES (source.KnownEndpointId, source.Name, source.HostId, source.Host, 0);
            """;

        var rowsAffected = await dbContext.Database.ExecuteSqlRawAsync(sql, [new Microsoft.Data.SqlClient.SqlParameter("@batchSize", batchSize)], cancellationToken);
        return rowsAffected;
    }
}
