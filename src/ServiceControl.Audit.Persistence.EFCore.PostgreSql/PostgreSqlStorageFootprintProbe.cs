namespace ServiceControl.Audit.Persistence.EFCore.PostgreSql;

using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceControl.Audit.Persistence.EFCore.Abstractions;
using ServiceControl.Audit.Persistence.EFCore.DbContexts;
using ServiceControl.Audit.Persistence.EFCore.Entities;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;

class PostgreSqlStorageFootprintProbe(EFPersisterSettings settings, IServiceScopeFactory scopeFactory, ILogger<PostgreSqlStorageFootprintProbe> logger) : IStorageFootprintProbe
{
    public async Task<StorageFootprint?> Probe(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            var auditMessagesTable = dbContext.Model.FindEntityType(typeof(AuditMessageEntity))?.GetTableName();

            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            // The audit messages table is partitioned by day, and the parent carries no row estimate,
            // so the estimate is summed over the partitions that have been analysed.
            command.CommandText = """
                SELECT
                    (SELECT SUM(pg_total_relation_size(c.oid)) / 1073741824.0
                     FROM pg_class c
                     JOIN pg_namespace n ON n.oid = c.relnamespace
                     WHERE n.nspname = COALESCE(@schema, current_schema()) AND c.relkind IN ('r', 'm')),
                    (SELECT CASE WHEN bool_and(c.reltuples < 0) THEN NULL ELSE SUM(GREATEST(c.reltuples, 0))::bigint END
                     FROM pg_partition_tree(to_regclass(format('%I.%I', COALESCE(@schema, current_schema()), @table))) t
                     JOIN pg_class c ON c.oid = t.relid
                     WHERE t.isleaf)
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
            logger.LogDebug(e, "Could not read the PostgreSQL storage footprint");

            return null;
        }
    }

    const int ProbeTimeoutSeconds = 30;
}
