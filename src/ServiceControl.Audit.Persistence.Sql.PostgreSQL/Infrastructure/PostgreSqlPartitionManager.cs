namespace ServiceControl.Audit.Persistence.Sql.PostgreSQL.Infrastructure;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Audit.Persistence.Sql.Core.DbContexts;
using ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

// Partition/table names cannot be parameterized in SQL; all values come from internal constants and date formatting
#pragma warning disable EF1002, EF1003
public class PostgreSqlPartitionManager : IPartitionManager
{
    static readonly (string ParentTable, string Prefix)[] PartitionedTables =
    [
        ("processed_messages", "processed_messages"),
        ("saga_snapshots", "saga_snapshots")
    ];

    public async Task EnsurePartitionsExist(AuditDbContextBase dbContext, DateTime currentHour, int hoursAhead, CancellationToken ct)
    {
        var truncatedHour = TruncateToHour(currentHour);
        var targetHour = truncatedHour.AddHours(hoursAhead);

        for (var hour = truncatedHour; hour <= targetHour; hour = hour.AddHours(1))
        {
            var nextHour = hour.AddHours(1);
            var hourSuffix = hour.ToString("yyyyMMddHH");
            var hourStr = hour.ToString("yyyy-MM-dd HH:00:00");
            var nextHourStr = nextHour.ToString("yyyy-MM-dd HH:00:00");

            foreach (var (parentTable, prefix) in PartitionedTables)
            {
                var partitionName = prefix + "_" + hourSuffix;

                await dbContext.Database.ExecuteSqlRawAsync(
                    "CREATE TABLE IF NOT EXISTS " + partitionName +
                    " PARTITION OF " + parentTable +
                    " FOR VALUES FROM ('" + hourStr + "') TO ('" + nextHourStr + "')", ct);
            }
        }
    }

    public async Task DropPartition(AuditDbContextBase dbContext, DateTime partitionHour, CancellationToken ct)
    {
        var hourSuffix = TruncateToHour(partitionHour).ToString("yyyyMMddHH");

        foreach (var (parentTable, prefix) in PartitionedTables)
        {
            var partitionName = prefix + "_" + hourSuffix;

            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE " + parentTable + " DETACH PARTITION " + partitionName, ct);

            await dbContext.Database.ExecuteSqlRawAsync(
                "DROP TABLE " + partitionName, ct);
        }
    }

    public async Task<List<DateTime>> GetExpiredPartitions(AuditDbContextBase dbContext, DateTime cutoff, CancellationToken ct)
    {
        var truncatedCutoff = TruncateToHour(cutoff);

        var partitionNames = await dbContext.Database
            .SqlQueryRaw<string>(
                "SELECT c.relname AS Value " +
                "FROM pg_class c " +
                "INNER JOIN pg_inherits i ON c.oid = i.inhrelid " +
                "INNER JOIN pg_class parent ON i.inhparent = parent.oid " +
                "WHERE parent.relname = 'processed_messages' " +
                "AND c.relkind = 'r' " +
                "ORDER BY c.relname")
            .ToListAsync(ct);

        var result = new List<DateTime>();

        foreach (var name in partitionNames)
        {
            // Parse hour from partition name: processed_messages_yyyyMMddHH
            var datePart = name.Replace("processed_messages_", "");
            if (DateTime.TryParseExact(datePart, "yyyyMMddHH", null, System.Globalization.DateTimeStyles.None, out var hour)
                && hour < truncatedCutoff)
            {
                result.Add(hour);
            }
        }

        return result;
    }

    static DateTime TruncateToHour(DateTime dt) => new(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0, dt.Kind);
}
