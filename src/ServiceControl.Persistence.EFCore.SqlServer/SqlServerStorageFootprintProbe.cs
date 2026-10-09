namespace ServiceControl.Persistence.EFCore.SqlServer;

using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

class SqlServerStorageFootprintProbe(SqlServerPersisterSettings settings, IServiceScopeFactory scopeFactory, ILogger<SqlServerStorageFootprintProbe> logger) : IStorageFootprintProbe
{
    public async Task<StorageFootprint?> Probe(CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await Read(cancellationToken);
            }
            catch (SqlException e) when (attempt < MaxAttempts && e.Number is DeadlockVictim or ScanLostItsPlace)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                logger.LogDebug(e, "Could not read the SQL Server storage footprint");

                return null;
            }
        }
    }

    async Task<StorageFootprint?> Read(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        var failedMessagesTable = dbContext.Model.FindEntityType(typeof(FailedMessageEntity))?.GetTableName();

        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        // Full-text index fragments live in internal tables under the indexed table, so they are counted too.
        // Catalog reads under READ COMMITTED deadlock with concurrent DDL. Under READ UNCOMMITTED a scan can
        // instead lose its place when pages move, so Probe retries both errors.
        command.CommandText = """
            SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
            DECLARE @resolved sysname = COALESCE(@schema, SCHEMA_NAME());
            SELECT
                (SELECT SUM(a.total_pages) * 8.0 / 1048576
                 FROM sys.partitions p
                 JOIN sys.allocation_units a ON a.container_id = p.partition_id
                 WHERE p.object_id IN (
                     SELECT t.object_id FROM sys.tables t WHERE t.schema_id = SCHEMA_ID(@resolved)
                     UNION ALL
                     SELECT it.object_id FROM sys.internal_tables it JOIN sys.tables t ON t.object_id = it.parent_id WHERE t.schema_id = SCHEMA_ID(@resolved))),
                (SELECT SUM(p.rows)
                 FROM sys.tables t
                 JOIN sys.partitions p ON p.object_id = t.object_id
                 WHERE t.schema_id = SCHEMA_ID(@resolved) AND t.name = @table AND p.index_id IN (0, 1))
            """;
        command.CommandTimeout = ProbeTimeoutSeconds;

        var schemaParameter = command.CreateParameter();
        schemaParameter.ParameterName = "@schema";
        schemaParameter.DbType = DbType.String;
        schemaParameter.Value = (object?)settings.Schema ?? DBNull.Value;
        command.Parameters.Add(schemaParameter);

        var tableParameter = command.CreateParameter();
        tableParameter.ParameterName = "@table";
        tableParameter.Value = failedMessagesTable ?? "";
        command.Parameters.Add(tableParameter);

        await dbContext.Database.OpenConnectionAsync(cancellationToken);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new StorageFootprint(
            reader.IsDBNull(0) ? null : Convert.ToDouble(reader.GetValue(0), CultureInfo.InvariantCulture),
            reader.IsDBNull(1) ? null : Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture));
    }

    const int ProbeTimeoutSeconds = 30;
    const int MaxAttempts = 3;
    const int DeadlockVictim = 1205;
    const int ScanLostItsPlace = 601;
}
