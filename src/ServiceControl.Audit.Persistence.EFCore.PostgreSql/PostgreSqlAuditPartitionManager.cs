namespace ServiceControl.Audit.Persistence.EFCore.PostgreSql;

using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;
using ServiceControl.Audit.Persistence.EFCore.DbContexts;
using ServiceControl.Audit.Persistence.EFCore.Entities;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;

class PostgreSqlAuditPartitionManager(ILogger<PostgreSqlAuditPartitionManager> logger) : IAuditPartitionManager
{
    public async Task EnsurePartitions(AuditDbContext dbContext, DateTime from, DateTime toExclusive, CancellationToken cancellationToken = default)
    {
        var sql = new StringBuilder();
        foreach (var table in Tables(dbContext))
        {
            for (var day = from.Date; day < toExclusive; day = day.AddDays(1))
            {
                sql.Append(CultureInfo.InvariantCulture, $"CREATE TABLE IF NOT EXISTS {table.Partition(day)} PARTITION OF {table.Parent} FOR VALUES FROM ('{Bound(day)}') TO ('{Bound(day.AddDays(1))}');\n");
            }
        }

        if (sql.Length == 0)
        {
            return;
        }

        if (!await WithShortLockWait(dbContext, sql.ToString(), cancellationToken))
        {
            logger.LogWarning("Could not provision audit partitions up to {End:u} because the audit tables were too busy. The next sweep will try again", toExclusive);
        }
    }

    public async Task<IReadOnlyList<DateTime>> ListExpired(AuditDbContext dbContext, DateTime keepFrom, CancellationToken cancellationToken = default)
    {
        var days = new SortedSet<DateTime>();
        foreach (var table in Tables(dbContext))
        {
            foreach (var day in await PartitionDays(dbContext, table, cancellationToken))
            {
                if (day.AddDays(1) <= keepFrom)
                {
                    days.Add(day);
                }
            }
        }

        return [.. days];
    }

    public async Task<DropResult> DropExpired(AuditDbContext dbContext, DateTime day, int batchSize, CancellationToken cancellationToken = default)
    {
        var sql = string.Concat(Tables(dbContext).Select(table => $"DROP TABLE IF EXISTS {table.Partition(day)};\n"));

        return await WithShortLockWait(dbContext, sql, cancellationToken)
            ? new DropResult(RowsDeleted: 0, Completed: true)
            : new DropResult(RowsDeleted: 0, Completed: true, Deferred: true);
    }

    public async Task<DateTime?> ProvisionedUntil(AuditDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var days = await PartitionDays(dbContext, Table<AuditMessageEntity>(dbContext), cancellationToken);
        return days.Count == 0 ? null : days.Max().AddDays(1);
    }

    internal static string PartitionName(string tableName, DateTime day) =>
        string.Create(CultureInfo.InvariantCulture, $"{tableName}_{day:yyyyMMdd}");

    static async Task<List<DateTime>> PartitionDays(AuditDbContext dbContext, AuditTable table, CancellationToken cancellationToken)
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

        var days = new List<DateTime>(names.Count);
        foreach (var name in names)
        {
            if (name.Length > table.Name.Length + 1
                && DateTime.TryParseExact(name[(table.Name.Length + 1)..], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var day))
            {
                days.Add(day);
            }
        }

        return days;
    }

    // While DDL waits for its exclusive lock, every insert and query queues behind it.
    static Task<bool> WithShortLockWait(AuditDbContext dbContext, string sql, CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(token);
            await dbContext.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'", token);

            try
            {
                await dbContext.Database.ExecuteSqlRawAsync(sql, token);
            }
            catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.LockNotAvailable)
            {
                return false;
            }

            await transaction.CommitAsync(token);
            return true;
        }, cancellationToken);

    static AuditTable[] Tables(AuditDbContext dbContext) => [Table<AuditMessageEntity>(dbContext), Table<SagaSnapshotEntity>(dbContext)];

    static AuditTable Table<TEntity>(AuditDbContext dbContext) =>
        new(dbContext.Model.FindEntityType(typeof(TEntity))!.GetTableName()!, SchemaQualifiedTableName.For<TEntity>(dbContext), dbContext.Schema, dbContext.GetService<ISqlGenerationHelper>());

    static string Bound(DateTime day) => day.ToString("yyyy-MM-dd 00:00:00+00", CultureInfo.InvariantCulture);

    sealed record AuditTable(string Name, string Parent, string? Schema, ISqlGenerationHelper SqlGenerationHelper)
    {
        public string Partition(DateTime day) => SqlGenerationHelper.DelimitIdentifier(PartitionName(Name, day), Schema);
    }
}
