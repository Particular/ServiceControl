namespace ServiceControl.Audit.Persistence.Sql.SqlServer.Infrastructure;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Audit.Persistence.Sql.Core.DbContexts;
using ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

// Partition/table names cannot be parameterized in SQL; all values come from internal constants and date formatting
#pragma warning disable EF1002, EF1003
public class SqlServerPartitionManager : IPartitionManager
{
    const string PartitionFunctionName = "pf_ProcessedAt";
    const string PartitionSchemeName = "ps_ProcessedAt";

    static readonly string[] PartitionedTables = ["ProcessedMessages", "SagaSnapshots"];

    public async Task EnsurePartitionsExist(AuditDbContextBase dbContext, DateTime currentDate, int daysAhead, CancellationToken ct)
    {
        var existingBoundaries = await GetExistingBoundaries(dbContext, ct);
        var targetDate = currentDate.Date.AddDays(daysAhead);

        for (var date = currentDate.Date; date <= targetDate; date = date.AddDays(1))
        {
            if (existingBoundaries.Contains(date))
            {
                continue;
            }

            var dateStr = date.ToString("yyyy-MM-dd");

            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER PARTITION SCHEME " + PartitionSchemeName + " NEXT USED [PRIMARY]", ct);

            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER PARTITION FUNCTION " + PartitionFunctionName + "() SPLIT RANGE ('" + dateStr + "')", ct);
        }
    }

    public async Task DropPartition(AuditDbContextBase dbContext, DateTime partitionDate, CancellationToken ct)
    {
        var partitionNumber = await GetPartitionNumber(dbContext, partitionDate, ct);
        if (partitionNumber == null)
        {
            return;
        }

        var dateStr = partitionDate.ToString("yyyy-MM-dd");

        foreach (var table in PartitionedTables)
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "TRUNCATE TABLE " + table + " WITH (PARTITIONS(" + partitionNumber + "))", ct);
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER PARTITION FUNCTION " + PartitionFunctionName + "() MERGE RANGE ('" + dateStr + "')", ct);
    }

    public async Task<List<DateTime>> GetExpiredPartitionDates(AuditDbContextBase dbContext, DateTime cutoff, CancellationToken ct)
    {
        var boundaries = await dbContext.Database
            .SqlQueryRaw<DateTime>(
                "SELECT CAST(prv.value AS datetime2) AS Value " +
                "FROM sys.partition_range_values prv " +
                "INNER JOIN sys.partition_functions pf ON prv.function_id = pf.function_id " +
                "WHERE pf.name = '" + PartitionFunctionName + "' " +
                "AND CAST(prv.value AS datetime2) < {0} " +
                "ORDER BY prv.value", cutoff.Date)
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

        return [.. boundaries.Select(b => b.Date)];
    }

    async Task<int?> GetPartitionNumber(AuditDbContextBase dbContext, DateTime partitionDate, CancellationToken ct)
    {
        var dateStr = partitionDate.ToString("yyyy-MM-dd");

        var result = await dbContext.Database
            .SqlQueryRaw<int>(
                "SELECT $PARTITION." + PartitionFunctionName + "('" + dateStr + "') AS Value")
            .ToListAsync(ct);

        return result.Count > 0 ? result[0] : null;
    }
}
