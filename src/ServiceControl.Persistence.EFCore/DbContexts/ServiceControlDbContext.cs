namespace ServiceControl.Persistence.EFCore.DbContexts;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.EntityConfigurations;

public abstract class ServiceControlDbContext(DbContextOptions options) : DbContext(options)
{
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
    public DbSet<LicensingEndpointEntity> LicensingEndpoints { get; set; }
    public DbSet<LicensingEndpointThroughputEntity> LicensingEndpointThroughput { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.EnableDetailedErrors();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new CustomCheckConfiguration());
        modelBuilder.ApplyConfiguration(new EndpointSettingsConfiguration());
        modelBuilder.ApplyConfiguration(new FailedErrorImportConfiguration());
        modelBuilder.ApplyConfiguration(new FailedMessageConfiguration());
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
    }

    public abstract bool IsDuplicateKeyException(DbUpdateException exception);
}
