namespace ServiceControl.Audit.Persistence.Sql.SqlServer.Infrastructure;

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
                Host NVARCHAR(MAX),
                LastSeen DATETIME2
            );

            DELETE TOP (@batchSize) FROM KnownEndpointsInsertOnly
            OUTPUT DELETED.KnownEndpointId, DELETED.Name, DELETED.HostId, DELETED.Host, DELETED.LastSeen
            INTO @deleted;

            WITH aggregated AS (
                SELECT KnownEndpointId, Name, HostId, Host, MAX(LastSeen) AS LastSeen
                FROM @deleted
                GROUP BY KnownEndpointId, Name, HostId, Host
            )
            MERGE INTO KnownEndpoints AS target
            USING aggregated AS source
            ON target.Id = source.KnownEndpointId
            WHEN MATCHED AND source.LastSeen > target.LastSeen THEN
                UPDATE SET LastSeen = source.LastSeen
            WHEN NOT MATCHED THEN
                INSERT (Id, Name, HostId, Host, LastSeen)
                VALUES (source.KnownEndpointId, source.Name, source.HostId, source.Host, source.LastSeen);

            SELECT @@ROWCOUNT;
        ";

        var rowsAffected = await dbContext.Database.ExecuteSqlRawAsync(sql, [new Microsoft.Data.SqlClient.SqlParameter("@batchSize", BatchSize)], stoppingToken);
        return rowsAffected;
    }
}
