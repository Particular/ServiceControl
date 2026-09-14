namespace ServiceControl.Persistence.EFCore.PostgreSql.Audit;

using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Implementation.Audit;
using ServiceControl.Persistence.EFCore.Infrastructure;

// One statement per hour per table, all in one command, so provisioning a two day window is a
// single round trip. CREATE TABLE IF NOT EXISTS makes a re-run over an existing window harmless.
class PostgreSqlAuditPartitionManager : IAuditPartitionManager
{
    public async Task EnsurePartitions(ServiceControlDbContext dbContext, DateTime fromHour, DateTime toHourExclusive, CancellationToken cancellationToken = default)
    {
        var sql = new StringBuilder();

        AppendPartitions<AuditMessageEntity>(sql, dbContext, fromHour, toHourExclusive);
        AppendPartitions<SagaSnapshotEntity>(sql, dbContext, fromHour, toHourExclusive);

        if (sql.Length == 0)
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(sql.ToString(), cancellationToken);
    }

    static void AppendPartitions<TEntity>(StringBuilder sql, ServiceControlDbContext dbContext, DateTime fromHour, DateTime toHourExclusive)
    {
        var parent = SchemaQualifiedTableName.For<TEntity>(dbContext);
        var tableName = dbContext.Model.FindEntityType(typeof(TEntity))!.GetTableName()!;
        var sqlGenerationHelper = dbContext.GetService<ISqlGenerationHelper>();

        for (var hour = AuditHours.Truncate(fromHour); hour < toHourExclusive; hour = hour.AddHours(1))
        {
            var partition = sqlGenerationHelper.DelimitIdentifier(AuditHours.PartitionName(tableName, hour), dbContext.Schema);

            sql.Append($"CREATE TABLE IF NOT EXISTS {partition} PARTITION OF {parent} FOR VALUES FROM ('{Bound(hour)}') TO ('{Bound(hour.AddHours(1))}');\n");
        }
    }

    static string Bound(DateTime hour) => hour.ToString("yyyy-MM-dd HH:00:00+00");
}
