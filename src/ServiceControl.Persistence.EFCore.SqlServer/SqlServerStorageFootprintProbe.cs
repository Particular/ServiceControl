namespace ServiceControl.Persistence.EFCore.SqlServer;

using System.Globalization;
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
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
            var failedMessagesTable = dbContext.Model.FindEntityType(typeof(FailedMessageEntity))?.GetTableName();
            var schema = settings.Schema ?? "dbo";

            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT
                    (SELECT SUM(a.total_pages) * 8.0 / 1048576
                     FROM sys.tables t
                     JOIN sys.schemas s ON s.schema_id = t.schema_id
                     JOIN sys.partitions p ON p.object_id = t.object_id
                     JOIN sys.allocation_units a ON a.container_id = p.partition_id
                     WHERE s.name = @schema),
                    (SELECT SUM(p.rows)
                     FROM sys.tables t
                     JOIN sys.schemas s ON s.schema_id = t.schema_id
                     JOIN sys.partitions p ON p.object_id = t.object_id
                     WHERE s.name = @schema AND t.name = @table AND p.index_id IN (0, 1))
                """;
            command.CommandTimeout = ProbeTimeoutSeconds;

            var schemaParameter = command.CreateParameter();
            schemaParameter.ParameterName = "@schema";
            schemaParameter.Value = schema;
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
