namespace ServiceControl.Persistence.EFCore.DbContexts;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.EntityConfigurations;
using ServiceControl.Persistence.EFCore.Infrastructure;

public abstract class ServiceControlDbContext(DbContextOptions options) : DbContext(options)
{
    // Not EF Core's default, so it identifies an audit instance that wrongly shares this schema.
    const string AuditMigrationsHistoryTableName = "__AuditMigrationsHistory";

    /// <summary>
    /// The configured schema, or null when the provider default is in use. Null keeps the model
    /// identical to the one the migrations were scaffolded against.
    /// </summary>
    public string? Schema { get; } = options.FindExtension<SchemaOptionsExtension>()?.Schema;

    public DbSet<CustomCheckEntity> CustomChecks { get; set; }
    public DbSet<EndpointSettingsEntity> EndpointSettings { get; set; }
    public DbSet<KnownEndpointEntity> KnownEndpoints { get; set; }
    public DbSet<FailedMessageEntity> FailedMessages { get; set; }
    public DbSet<FailedMessageGroupEntity> FailedMessageGroups { get; set; }
    public DbSet<GroupCommentEntity> GroupComments { get; set; }
    public DbSet<MessageRedirectEntity> MessageRedirects { get; set; }
    public DbSet<RetryBatchEntity> RetryBatches { get; set; }
    public DbSet<RetryBatchNowForwardingEntity> RetryBatchNowForwarding { get; set; }
    public DbSet<FailedMessageRetryEntity> FailedMessageRetries { get; set; }
    public DbSet<FailedErrorImportEntity> FailedErrorImports { get; set; }
    public DbSet<SettingEntity> Settings { get; set; }
    public DbSet<SubscriptionEntity> Subscriptions { get; set; }
    public DbSet<EventLogItemEntity> EventLogItems { get; set; }
    public DbSet<HistoricRetryOperationEntity> HistoricRetryOperations { get; set; }
    public DbSet<UnacknowledgedRetryOperationEntity> UnacknowledgedRetryOperations { get; set; }
    public DbSet<ArchiveOperationEntity> ArchiveOperations { get; set; }
    public DbSet<FailedMessageEditEntity> FailedMessageEdits { get; set; }
    public DbSet<LicensingEndpointEntity> LicensingEndpoints { get; set; }
    public DbSet<LicensingEndpointThroughputEntity> LicensingEndpointThroughput { get; set; }
    public DbSet<ExternalIntegrationDispatchRequestEntity> ExternalIntegrationDispatchRequests { get; set; }

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

        modelBuilder.ApplyConfiguration(new CustomCheckConfiguration());
        modelBuilder.ApplyConfiguration(new EndpointSettingsConfiguration());
        modelBuilder.ApplyConfiguration(new FailedErrorImportConfiguration());
        modelBuilder.ApplyConfiguration(new FailedMessageConfiguration());
        modelBuilder.ApplyConfiguration(new FailedMessageEditConfiguration());
        modelBuilder.ApplyConfiguration(new FailedMessageGroupConfiguration());
        modelBuilder.ApplyConfiguration(new FailedMessageRetryConfiguration());
        modelBuilder.ApplyConfiguration(new GroupCommentConfiguration());
        modelBuilder.ApplyConfiguration(new MessageRedirectConfiguration());
        modelBuilder.ApplyConfiguration(new RetryBatchConfiguration());
        modelBuilder.ApplyConfiguration(new RetryBatchNowForwardingConfiguration());
        modelBuilder.ApplyConfiguration(new KnownEndpointConfiguration());
        modelBuilder.ApplyConfiguration(new SubscriptionConfiguration());
        modelBuilder.ApplyConfiguration(new SettingConfiguration());
        modelBuilder.ApplyConfiguration(new EventLogItemConfiguration());
        modelBuilder.ApplyConfiguration(new HistoricRetryOperationConfiguration());
        modelBuilder.ApplyConfiguration(new UnacknowledgedRetryOperationConfiguration());
        modelBuilder.ApplyConfiguration(new ArchiveOperationConfiguration());
        modelBuilder.ApplyConfiguration(new LicensingEndpointConfiguration());
        modelBuilder.ApplyConfiguration(new LicensingEndpointThroughputConfiguration());
        modelBuilder.ApplyConfiguration(new ExternalIntegrationDispatchRequestConfiguration());
    }

    public abstract bool IsDuplicateKeyException(DbUpdateException exception);

    /// <summary>
    /// Whether the table exists in the configured schema, or in the provider default schema when none is configured.
    /// </summary>
    protected abstract Task<bool> TableExists(string table, CancellationToken cancellationToken = default);

    public async Task RequireNoAuditMigrationsHistory(CancellationToken cancellationToken = default)
    {
        if (await TableExists(AuditMigrationsHistoryTableName, cancellationToken))
        {
            throw new InvalidOperationException(
                $"The schema '{Schema ?? "(provider default)"}' already contains the audit migrations history table '{AuditMigrationsHistoryTableName}'. A primary instance and an audit instance cannot share a schema. Configure a different Database/Schema for this instance, create the schema, grant the configured user rights on it, and run setup again.");
        }
    }
}
