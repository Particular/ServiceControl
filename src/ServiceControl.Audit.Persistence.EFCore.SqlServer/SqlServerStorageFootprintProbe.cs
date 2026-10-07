namespace ServiceControl.Audit.Persistence.EFCore.SqlServer;

using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceControl.Audit.Persistence.EFCore.Abstractions;
using ServiceControl.Audit.Persistence.EFCore.DbContexts;
using ServiceControl.Audit.Persistence.EFCore.Entities;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;

class SqlServerStorageFootprintProbe(EFPersisterSettings settings, IServiceScopeFactory scopeFactory, ILogger<SqlServerStorageFootprintProbe> logger) : IStorageFootprintProbe
{
    public async Task<StorageFootprint?> Probe(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            var auditMessagesTable = dbContext.Model.FindEntityType(typeof(AuditMessageEntity))?.GetTableName();

            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            // Full-text index fragments live in internal tables under the indexed table, so they are counted too.
            command.CommandText = """
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
            tableParameter.Value = auditMessagesTable ?? "";
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

    const int ProbeTimeoutSeconds = 30;
}
