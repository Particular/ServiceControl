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

        // I used this setting when testing in more budget constrained environments, and it seems to help prevent timeouts when deleting large amounts of data.
        // But since then We have also concluded that those budget environments are not fit for running ServiceControl.
        // I am coming to the conclusion that we should leve the default 30 seconds, and if we see issues in the logs it means the environment needs to be scaled up!
        dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));

        foreach (var table in Tables)
        {
            while (true)
            {
                // The comment below is accurate, but after testing, I have come to the conclusion that the performance benefits of deleting in batches of 4500 rows are actually not ideal, 
                // and that it is better to delete all rows in one go, even if it means that we might get a lock escalation to a full table lock.
                // In our testing the retention deletion doubled in time when we switched to deleting in batches of 4500 rows.
                // And no noticeable decrease in ingestion performance was observed when we switched to deleting all rows in one go, even with the lock escalation.

                // Prevent lock escalation to a full table lock by deleting in batches of 4500 rows, 
                // the rule of thumb is that a lock escalation occurs when a transaction acquires more than 5000 locks. 
                // Deleting in batches of 4500 rows allows us to stay below this threshold and avoid escalating to a full table lock, 
                // which can improve concurrency and reduce contention with other transactions.
                var deleted = await dbContext.Database.ExecuteSqlRawAsync(
                    "DELETE TOP (4500) FROM " + table + " WHERE CreatedOn >= '" + hourStr + "' AND CreatedOn < '" + nextHourStr + "'", ct);

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
