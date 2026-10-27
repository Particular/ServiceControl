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

    protected override async Task<bool> TableExists(string table, CancellationToken cancellationToken = default) =>
        await Database
            .SqlQueryRaw<int>("SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = {0} AND schema_id = SCHEMA_ID(COALESCE(CAST({1} AS sysname), SCHEMA_NAME()))) THEN 1 ELSE 0 END AS [Value]", table, Schema ?? (object)DBNull.Value)
            .SingleAsync(cancellationToken) == 1;
}
