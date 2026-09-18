namespace ServiceControl.Persistence.EFCore.PostgreSql.Audit;

using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Implementation.Audit;
using ServiceControl.Persistence.EFCore.Infrastructure;

// Partitions are named {table}_{yyyyMMddHH}, which is what makes an hour's partition addressable
// without a catalog lookup and makes the catalog listing parseable back into hours.
class PostgreSqlAuditPartitionManager : IAuditPartitionManager
{
    // One statement per hour per table, all in one command, so provisioning a two day window is a
    // single round trip. CREATE TABLE IF NOT EXISTS makes a re-run over an existing window harmless.
    public async Task EnsurePartitions(ServiceControlDbContext dbContext, DateTime fromHour, DateTime toHourExclusive, CancellationToken cancellationToken = default)
    {
        var sql = new StringBuilder();

        foreach (var table in Tables(dbContext))
        {
            for (var hour = AuditHours.Truncate(fromHour); hour < toHourExclusive; hour = hour.AddHours(1))
            {
                sql.Append($"CREATE TABLE IF NOT EXISTS {table.Partition(hour)} PARTITION OF {table.Parent} FOR VALUES FROM ('{Bound(hour)}') TO ('{Bound(hour.AddHours(1))}');\n");
            }
        }

        if (sql.Length == 0)
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(sql.ToString(), cancellationToken);
    }

    public async Task<IReadOnlyList<DateTime>> ListExpiredHours(ServiceControlDbContext dbContext, DateTime lastExpiredHour, CancellationToken cancellationToken = default)
    {
        var hours = new SortedSet<DateTime>();

        foreach (var table in Tables(dbContext))
        {
            foreach (var hour in await PartitionHours(dbContext, table, cancellationToken))
            {
                if (hour <= lastExpiredHour)
                {
                    hours.Add(hour);
                }
            }
        }

        return [.. hours];
    }

    // Dropping a partition outright is a metadata operation: the rows go with it, and the parent is
    // locked for the moment it takes. Bodies are already gone by the time this runs.
    public async Task<HourDrop> DropHour(ServiceControlDbContext dbContext, DateTime hour, int batchSize, CancellationToken cancellationToken = default)
    {
        foreach (var table in Tables(dbContext))
        {
            var sql = "DROP TABLE IF EXISTS " + table.Partition(hour);

            await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }

        return new HourDrop(RowsDeleted: 0, Completed: true);
    }

    public async Task<DateTime?> NewestProvisionedHourEnd(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var hours = await PartitionHours(dbContext, Table<AuditMessageEntity>(dbContext), cancellationToken);

        return hours.Count == 0 ? null : hours.Max().AddHours(1);
    }

    static async Task<List<DateTime>> PartitionHours(ServiceControlDbContext dbContext, AuditTable table, CancellationToken cancellationToken)
    {
        var names = await dbContext.Database
            .SqlQueryRaw<string>("""
                SELECT c.relname AS "Value"
                FROM pg_inherits i
                JOIN pg_class c ON c.oid = i.inhrelid
                JOIN pg_class p ON p.oid = i.inhparent
                JOIN pg_namespace n ON n.oid = p.relnamespace
                WHERE p.relname = {0} AND n.nspname = COALESCE(CAST({1} AS text), current_schema())
                """, table.Name, dbContext.Schema ?? (object)DBNull.Value)
            .ToListAsync(cancellationToken);

        var hours = new List<DateTime>(names.Count);

        foreach (var name in names)
        {
            if (name.Length > table.Name.Length + 1
                && DateTime.TryParseExact(name[(table.Name.Length + 1)..], "yyyyMMddHH", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var hour))
            {
                hours.Add(hour);
            }
        }

        return hours;
    }

    static AuditTable[] Tables(ServiceControlDbContext dbContext) => [Table<AuditMessageEntity>(dbContext), Table<SagaSnapshotEntity>(dbContext)];

    static AuditTable Table<TEntity>(ServiceControlDbContext dbContext) =>
        new(dbContext.Model.FindEntityType(typeof(TEntity))!.GetTableName()!, SchemaQualifiedTableName.For<TEntity>(dbContext), dbContext.Schema, dbContext.GetService<ISqlGenerationHelper>());

    static string Bound(DateTime hour) => hour.ToString("yyyy-MM-dd HH:00:00+00", CultureInfo.InvariantCulture);

    sealed record AuditTable(string Name, string Parent, string? Schema, ISqlGenerationHelper SqlGenerationHelper)
    {
        public string Partition(DateTime hour) => SqlGenerationHelper.DelimitIdentifier(AuditHours.PartitionName(Name, hour), Schema);
    }
}
