namespace ServiceControl.Audit.Persistence.EFCore.DbContexts;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Audit.Persistence.EFCore.Entities;
using ServiceControl.Audit.Persistence.EFCore.EntityConfigurations;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;

public abstract class AuditDbContext(DbContextOptions options) : DbContext(options)
{
    public const string MigrationsHistoryTableName = "__AuditMigrationsHistory";

    // The primary instance's migrations history table name, identified by ServiceControlDbContext.MigrationsHistoryTableName.
    const string PrimaryMigrationsHistoryTableName = "__ErrorMigrationsHistory";

    public string? Schema { get; } = options.FindExtension<SchemaOptionsExtension>()?.Schema;

    public DbSet<AuditMessageEntity> AuditMessages { get; set; }
    public DbSet<SagaSnapshotEntity> SagaSnapshots { get; set; }
    public DbSet<FailedAuditImportEntity> FailedAuditImports { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.EnableDetailedErrors();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

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

    /// <summary>
    /// Whether the table exists in the configured schema, or in the provider default schema when none is configured.
    /// </summary>
    protected abstract Task<bool> TableExists(string table, CancellationToken cancellationToken = default);

    public async Task RequireNoPrimaryMigrationsHistory(CancellationToken cancellationToken = default)
    {
        if (await TableExists(PrimaryMigrationsHistoryTableName, cancellationToken))
        {
            throw new InvalidOperationException(
                $"The schema '{Schema ?? "(provider default)"}' already contains the primary migrations history table '{PrimaryMigrationsHistoryTableName}'. A primary instance and an audit instance cannot share a schema. Configure a different Database/Schema for this instance and run setup again.");
        }
    }
}
