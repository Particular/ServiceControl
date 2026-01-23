namespace ServiceControl.Audit.Persistence.Sql.Core.DbContexts;

using Entities;
using EntityConfigurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public abstract class AuditDbContextBase : DbContext
{
    protected AuditDbContextBase(DbContextOptions options) : base(options)
    {
    }

    public DbSet<ProcessedMessageEntity> ProcessedMessages { get; set; }
    public DbSet<FailedAuditImportEntity> FailedAuditImports { get; set; }
    public DbSet<SagaSnapshotEntity> SagaSnapshots { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.LogTo(Console.WriteLine, LogLevel.Warning)
            .EnableDetailedErrors();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new ProcessedMessageConfiguration());
        modelBuilder.ApplyConfiguration(new FailedAuditImportConfiguration());
        modelBuilder.ApplyConfiguration(new SagaSnapshotConfiguration());

        OnModelCreatingProvider(modelBuilder);
    }

    protected virtual void OnModelCreatingProvider(ModelBuilder modelBuilder)
    {
    }
}
