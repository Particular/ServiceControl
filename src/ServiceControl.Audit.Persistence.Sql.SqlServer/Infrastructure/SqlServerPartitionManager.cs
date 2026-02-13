namespace ServiceControl.Audit.Persistence.Sql.SqlServer.Infrastructure;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Audit.Persistence.Sql.Core.Abstractions;
using ServiceControl.Audit.Persistence.Sql.Core.DbContexts;
using ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

// Partition/table names cannot be parameterized in SQL; all values come from internal constants and date formatting
#pragma warning disable EF1002, EF1003
public class SqlServerPartitionManager(MinimumRequiredStorageState storageState) : IPartitionManager
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
        var nextHourStr = truncatedHour.AddHours(1).ToString("yyyy-MM-ddTHH:00:00");

        // Cannot use TRUNCATE TABLE WITH (PARTITIONS(N)) because the full-text index
        // on ProcessedMessages is not partition-aligned and blocks the operation.
        // DELETE with a range filter benefits from partition elimination so only the
        // target partition is scanned.
        dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));

        storageState.CanIngestMore = false;
        try
        {
            foreach (var table in PartitionedTables)
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                    "DELETE FROM " + table + " WHERE CreatedOn >= '" + hourStr + "' AND CreatedOn < '" + nextHourStr + "'", ct);
            }

            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER PARTITION FUNCTION " + PartitionFunctionName + "() MERGE RANGE ('" + hourStr + "')", ct);

        }
        finally
        {
            storageState.CanIngestMore = true;
        }
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
