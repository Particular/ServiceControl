namespace ServiceControl.Audit.Persistence.EFCore.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ServiceControl.Audit.Persistence.EFCore.DbContexts;
using ServiceControl.Audit.Persistence.EFCore.Entities;

public class SqlServerAuditDbContext(DbContextOptions<SqlServerAuditDbContext> options) : AuditDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // The full text index needs a unique single column key, and the primary key has two columns.
        modelBuilder.Entity<AuditMessageEntity>().HasIndex(e => e.Id).IsUnique();
    }

    public override bool IsDuplicateKeyException(DbUpdateException exception)
    {
        for (var inner = exception.InnerException; inner != null; inner = inner.InnerException)
        {
            if (inner is SqlException { Number: 2601 or 2627 })
            {
                return true;
            }
        }

        return false;
    }

    public override async Task<bool> SchemaExists(string schema, CancellationToken cancellationToken = default) =>
        await Database
            .SqlQueryRaw<int>("SELECT CASE WHEN SCHEMA_ID({0}) IS NULL THEN 0 ELSE 1 END AS [Value]", schema)
            .SingleAsync(cancellationToken) == 1;
}
