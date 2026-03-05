namespace ServiceControl.Audit.Persistence.Sql.Core.DbContexts;

using Entities;
using EntityConfigurations;
using Microsoft.EntityFrameworkCore;

public abstract class AuditDbContextBase : DbContext
{
    protected AuditDbContextBase(DbContextOptions options) : base(options)
    {
    }

    public DbSet<ProcessedMessageEntity> ProcessedMessages { get; set; }
    public DbSet<FailedAuditImportEntity> FailedAuditImports { get; set; }
    public DbSet<SagaSnapshotEntity> SagaSnapshots { get; set; }
    public DbSet<KnownEndpointEntity> KnownEndpoints { get; set; }
    public DbSet<KnownEndpointInsertOnlyEntity> KnownEndpointsInsertOnly { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.EnableDetailedErrors();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new ProcessedMessageConfiguration());
        modelBuilder.ApplyConfiguration(new FailedAuditImportConfiguration());
        modelBuilder.ApplyConfiguration(new SagaSnapshotConfiguration());
        modelBuilder.ApplyConfiguration(new KnownEndpointConfiguration());
        modelBuilder.ApplyConfiguration(new KnownEndpointInsertOnlyConfiguration());

        OnModelCreatingProvider(modelBuilder);
    }

    protected virtual void OnModelCreatingProvider(ModelBuilder modelBuilder)
    {
    }
}
