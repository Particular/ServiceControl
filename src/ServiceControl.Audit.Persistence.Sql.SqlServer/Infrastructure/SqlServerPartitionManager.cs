namespace ServiceControl.Audit.Persistence.Sql.SqlServer.Infrastructure;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Audit.Persistence.Sql.Core.DbContexts;
using ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

// Table names cannot be parameterized in SQL; all values come from internal constants
#pragma warning disable EF1002, EF1003
public class SqlServerPartitionManager : IPartitionManager
{
    static readonly string[] Tables = ["ProcessedMessages", "SagaSnapshots"];

    public Task EnsurePartitionsExist(AuditDbContextBase dbContext, DateTime currentHour, int hoursAhead, CancellationToken ct)
    {
        // No partitioning on SQL Server — nothing to prepare
        return Task.CompletedTask;
    }

    public async Task DropPartition(AuditDbContextBase dbContext, DateTime partitionHour, CancellationToken ct)
    {
        var hourStr = TruncateToHour(partitionHour).ToString("yyyy-MM-ddTHH:00:00");
        var nextHourStr = TruncateToHour(partitionHour).AddHours(1).ToString("yyyy-MM-ddTHH:00:00");

        dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));

        foreach (var table in Tables)
        {
            while (true)
            {
                var deleted = await dbContext.Database.ExecuteSqlRawAsync(
                    "DELETE TOP (10000) FROM " + table + " WHERE CreatedOn >= '" + hourStr + "' AND CreatedOn < '" + nextHourStr + "'", ct);

                if (deleted == 0)
                {
                    break;
                }
            }
        }
    }

    public async Task<List<DateTime>> GetExpiredPartitions(AuditDbContextBase dbContext, DateTime cutoff, CancellationToken ct)
    {
        var truncatedCutoff = TruncateToHour(cutoff);

        // Find the oldest hour that has data, then return all hourly buckets up to the cutoff
        var oldestHours = await dbContext.Database
            .SqlQueryRaw<DateTime?>(
                "SELECT MIN(CreatedOn) AS Value FROM ProcessedMessages " +
                "UNION ALL " +
                "SELECT MIN(CreatedOn) AS Value FROM SagaSnapshots")
            .ToListAsync(ct);

        var oldest = oldestHours
            .Where(d => d.HasValue)
            .Select(d => TruncateToHour(d!.Value))
            .DefaultIfEmpty(truncatedCutoff)
            .Min();

        if (oldest >= truncatedCutoff)
        {
            return [];
        }

        var hours = new List<DateTime>();
        for (var hour = oldest; hour < truncatedCutoff; hour = hour.AddHours(1))
        {
            hours.Add(hour);
        }

        return hours;
    }

    static DateTime TruncateToHour(DateTime dt) => new(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0, dt.Kind);
}
