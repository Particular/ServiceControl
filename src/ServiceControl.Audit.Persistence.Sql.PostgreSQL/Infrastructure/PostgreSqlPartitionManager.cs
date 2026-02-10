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

    public async Task EnsurePartitionsExist(AuditDbContextBase dbContext, DateTime currentDate, int daysAhead, CancellationToken ct)
    {
        var targetDate = currentDate.Date.AddDays(daysAhead);

        for (var date = currentDate.Date; date <= targetDate; date = date.AddDays(1))
        {
            var nextDate = date.AddDays(1);
            var dateSuffix = date.ToString("yyyyMMdd");
            var dateStr = date.ToString("yyyy-MM-dd");
            var nextDateStr = nextDate.ToString("yyyy-MM-dd");

            foreach (var (parentTable, prefix) in PartitionedTables)
            {
                var partitionName = prefix + "_" + dateSuffix;

                await dbContext.Database.ExecuteSqlRawAsync(
                    "CREATE TABLE IF NOT EXISTS " + partitionName +
                    " PARTITION OF " + parentTable +
                    " FOR VALUES FROM ('" + dateStr + "') TO ('" + nextDateStr + "')", ct);
            }
        }
    }

    public async Task DropPartition(AuditDbContextBase dbContext, DateTime partitionDate, CancellationToken ct)
    {
        var dateSuffix = partitionDate.ToString("yyyyMMdd");

        foreach (var (parentTable, prefix) in PartitionedTables)
        {
            var partitionName = prefix + "_" + dateSuffix;

            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE " + parentTable + " DETACH PARTITION " + partitionName, ct);

            await dbContext.Database.ExecuteSqlRawAsync(
                "DROP TABLE " + partitionName, ct);
        }
    }

    public async Task<List<DateTime>> GetExpiredPartitionDates(AuditDbContextBase dbContext, DateTime cutoff, CancellationToken ct)
    {
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
            // Parse date from partition name: processed_messages_YYYYMMDD
            var datePart = name.Replace("processed_messages_", "");
            if (DateTime.TryParseExact(datePart, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date)
                && date < cutoff.Date)
            {
                result.Add(date);
            }
        }

        return result;
    }
}
