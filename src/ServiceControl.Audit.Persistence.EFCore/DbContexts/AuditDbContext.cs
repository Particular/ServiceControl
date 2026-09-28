namespace ServiceControl.Audit.Persistence.EFCore.DbContexts;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Audit.Persistence.EFCore.Entities;
using ServiceControl.Audit.Persistence.EFCore.EntityConfigurations;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;

public abstract class AuditDbContext(DbContextOptions options) : DbContext(options)
{
    // Not EF Core's default, so an audit instance can share a schema with a primary instance.
    public const string MigrationsHistoryTableName = "__AuditMigrationsHistory";

    public string? Schema { get; } = options.FindExtension<SchemaOptionsExtension>()?.Schema;

    public DbSet<AuditMessageEntity> AuditMessages { get; set; }
    public DbSet<SagaSnapshotEntity> SagaSnapshots { get; set; }
    public DbSet<FailedAuditImportEntity> FailedAuditImports { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.EnableDetailedErrors();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        if (Schema is not null)
        {
            modelBuilder.HasDefaultSchema(Schema);
        }

        modelBuilder.ApplyConfiguration(new AuditMessageConfiguration());
        modelBuilder.ApplyConfiguration(new SagaSnapshotConfiguration());
        modelBuilder.ApplyConfiguration(new FailedAuditImportConfiguration());
    }

    public abstract bool IsDuplicateKeyException(DbUpdateException exception);

    public abstract Task<bool> SchemaExists(string schema, CancellationToken cancellationToken = default);
}
