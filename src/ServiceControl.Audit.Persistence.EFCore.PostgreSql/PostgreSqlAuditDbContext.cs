namespace ServiceControl.Audit.Persistence.EFCore.PostgreSql;

using EntityConfigurations;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        SearchDocumentFunction.Apply(modelBuilder, Schema);

        modelBuilder.ApplyConfiguration(new AuditMessageConfiguration());
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

    protected override async Task<bool> TableExists(string table, CancellationToken cancellationToken = default) =>
        await Database
            .SqlQueryRaw<int>("""SELECT CASE WHEN EXISTS (SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE c.relkind = 'r' AND c.relname = {0} AND n.nspname = COALESCE(CAST({1} AS text), current_schema())) THEN 1 ELSE 0 END AS "Value" """, table, Schema ?? (object)DBNull.Value)
            .SingleAsync(cancellationToken) == 1;
}
