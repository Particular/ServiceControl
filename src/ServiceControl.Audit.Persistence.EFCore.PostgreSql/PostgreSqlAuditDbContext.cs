namespace ServiceControl.Audit.Persistence.EFCore.PostgreSql;

using Microsoft.EntityFrameworkCore;
using Npgsql;
using ServiceControl.Audit.Persistence.EFCore.DbContexts;

public class PostgreSqlAuditDbContext(DbContextOptions<PostgreSqlAuditDbContext> options) : AuditDbContext(options)
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        optionsBuilder.UseSnakeCaseNamingConvention();
    }

    public override bool IsDuplicateKeyException(DbUpdateException exception)
    {
        var queue = new Queue<Exception>([exception]);
        while (queue.Count > 0)
        {
            var e = queue.Dequeue();
            if (e is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return true;
            }

            if (e is AggregateException aggregateException)
            {
                foreach (var inner in aggregateException.InnerExceptions)
                {
                    queue.Enqueue(inner);
                }
            }

            if (e.InnerException != null)
            {
                queue.Enqueue(e.InnerException);
            }
        }

        return false;
    }

    public override async Task<bool> SchemaExists(string schema, CancellationToken cancellationToken = default) =>
        await Database
            .SqlQueryRaw<int>("""SELECT CASE WHEN EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = {0}) THEN 1 ELSE 0 END AS "Value" """, schema)
            .SingleAsync(cancellationToken) == 1;
}
