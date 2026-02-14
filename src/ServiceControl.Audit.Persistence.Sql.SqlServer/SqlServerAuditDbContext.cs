namespace ServiceControl.Audit.Persistence.Sql.SqlServer;

using Core.DbContexts;
using Core.Entities;
using Microsoft.EntityFrameworkCore;

public class SqlServerAuditDbContext : AuditDbContextBase
{
    public SqlServerAuditDbContext(DbContextOptions<SqlServerAuditDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreatingProvider(ModelBuilder modelBuilder)
    {
        // SQL Server doesn't use native partitioning, so it needs an index on CreatedOn
        // for efficient MIN() queries used by the retention cleaner
        modelBuilder.Entity<ProcessedMessageEntity>().HasIndex(e => e.CreatedOn);
        modelBuilder.Entity<SagaSnapshotEntity>().HasIndex(e => e.CreatedOn);
    }
}
