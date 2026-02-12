namespace ServiceControl.Audit.Persistence.Sql.SqlServer.Infrastructure;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Audit.Persistence.Sql.Core.DbContexts;
using ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

// Partition/table names cannot be parameterized in SQL; all values come from internal constants and date formatting
#pragma warning disable EF1002, EF1003
public class SqlServerPartitionManager : IPartitionManager
{
    const string PartitionFunctionName = "pf_CreatedOn";
    const string PartitionSchemeName = "ps_CreatedOn";

    static readonly string[] PartitionedTables = ["ProcessedMessages", "SagaSnapshots"];

    public async Task EnsurePartitionsExist(AuditDbContextBase dbContext, DateTime currentHour, int hoursAhead, CancellationToken ct)
    {
        var existingBoundaries = await GetExistingBoundaries(dbContext, ct);
        var truncatedHour = TruncateToHour(currentHour);
        var targetHour = truncatedHour.AddHours(hoursAhead);

        for (var hour = truncatedHour; hour <= targetHour; hour = hour.AddHours(1))
        {
            if (existingBoundaries.Contains(hour))
            {
                continue;
            }

            var hourStr = hour.ToString("yyyy-MM-ddTHH:00:00");

            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER PARTITION SCHEME " + PartitionSchemeName + " NEXT USED [PRIMARY]", ct);

            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER PARTITION FUNCTION " + PartitionFunctionName + "() SPLIT RANGE ('" + hourStr + "')", ct);
        }
    }

    public async Task DropPartition(AuditDbContextBase dbContext, DateTime partitionHour, CancellationToken ct)
    {
        var truncatedHour = TruncateToHour(partitionHour);
        var partitionNumber = await GetPartitionNumber(dbContext, truncatedHour, ct);
        if (partitionNumber == null)
        {
            return;
        }

        var hourStr = truncatedHour.ToString("yyyy-MM-ddTHH:00:00");

        foreach (var table in PartitionedTables)
        {
            var stagingTable = table + "_Staging";

            // Use SWITCH instead of TRUNCATE WITH (PARTITIONS(...)) because the latter
            // requires all indexes to be partition-aligned, which isn't possible when a
            // table has a full-text key index (must be single-column, can't include the
            // partitioning column). SWITCH is a metadata-only operation and has no such
            // restriction.
            await dbContext.Database.ExecuteSqlRawAsync(
                "IF OBJECT_ID('" + stagingTable + "', 'U') IS NOT NULL DROP TABLE " + stagingTable, ct);

            await dbContext.Database.ExecuteSqlRawAsync(
                "SELECT TOP(0) * INTO " + stagingTable + " FROM " + table, ct);

            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE " + table + " SWITCH PARTITION " + partitionNumber + " TO " + stagingTable, ct);

            await dbContext.Database.ExecuteSqlRawAsync(
                "DROP TABLE " + stagingTable, ct);
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER PARTITION FUNCTION " + PartitionFunctionName + "() MERGE RANGE ('" + hourStr + "')", ct);
    }

    public async Task<List<DateTime>> GetExpiredPartitions(AuditDbContextBase dbContext, DateTime cutoff, CancellationToken ct)
    {
        var truncatedCutoff = TruncateToHour(cutoff);

        var boundaries = await dbContext.Database
            .SqlQueryRaw<DateTime>(
                "SELECT CAST(prv.value AS datetime2) AS Value " +
                "FROM sys.partition_range_values prv " +
                "INNER JOIN sys.partition_functions pf ON prv.function_id = pf.function_id " +
                "WHERE pf.name = '" + PartitionFunctionName + "' " +
                "AND CAST(prv.value AS datetime2) < {0} " +
                "ORDER BY prv.value", truncatedCutoff)
            .ToListAsync(ct);

        return boundaries;
    }

    async Task<HashSet<DateTime>> GetExistingBoundaries(AuditDbContextBase dbContext, CancellationToken ct)
    {
        var boundaries = await dbContext.Database
            .SqlQueryRaw<DateTime>(
                "SELECT CAST(prv.value AS datetime2) AS Value " +
                "FROM sys.partition_range_values prv " +
                "INNER JOIN sys.partition_functions pf ON prv.function_id = pf.function_id " +
                "WHERE pf.name = '" + PartitionFunctionName + "' " +
                "ORDER BY prv.value")
            .ToListAsync(ct);

        return [.. boundaries.Select(TruncateToHour)];
    }

    async Task<int?> GetPartitionNumber(AuditDbContextBase dbContext, DateTime partitionHour, CancellationToken ct)
    {
        var hourStr = partitionHour.ToString("yyyy-MM-ddTHH:00:00");

        var result = await dbContext.Database
            .SqlQueryRaw<int>(
                "SELECT $PARTITION." + PartitionFunctionName + "('" + hourStr + "') AS Value")
            .ToListAsync(ct);

        return result.Count > 0 ? result[0] : null;
    }

    static DateTime TruncateToHour(DateTime dt) => new(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0, dt.Kind);
}
