namespace ServiceControl.Persistence.Sql.Core.DbContexts;

using Entities;
using EntityConfigurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public abstract class ServiceControlDbContextBase : DbContext
{
    protected ServiceControlDbContextBase(DbContextOptions options) : base(options)
    {
    }

    public DbSet<TrialLicenseEntity> TrialLicenses { get; set; }
    public DbSet<EndpointSettingsEntity> EndpointSettings { get; set; }
    public DbSet<EventLogItemEntity> EventLogItems { get; set; }
    public DbSet<MessageRedirectsEntity> MessageRedirects { get; set; }
    public DbSet<SubscriptionEntity> Subscriptions { get; set; }
    public DbSet<QueueAddressEntity> QueueAddresses { get; set; }
    public DbSet<KnownEndpointEntity> KnownEndpoints { get; set; }
    public DbSet<CustomCheckEntity> CustomChecks { get; set; }
    public DbSet<RetryHistoryEntity> RetryHistory { get; set; }
    public DbSet<FailedErrorImportEntity> FailedErrorImports { get; set; }
    public DbSet<ExternalIntegrationDispatchRequestEntity> ExternalIntegrationDispatchRequests { get; set; }
    public DbSet<ArchiveOperationEntity> ArchiveOperations { get; set; }
    public DbSet<FailedMessageEntity> FailedMessages { get; set; }
    public DbSet<RetryBatchEntity> RetryBatches { get; set; }
    public DbSet<FailedMessageRetryEntity> FailedMessageRetries { get; set; }
    public DbSet<GroupCommentEntity> GroupComments { get; set; }
    public DbSet<RetryBatchNowForwardingEntity> RetryBatchNowForwarding { get; set; }
    public DbSet<NotificationsSettingsEntity> NotificationsSettings { get; set; }
    public DbSet<LicensingMetadataEntity> LicensingMetadata { get; set; }
    public DbSet<ThroughputEndpointEntity> Endpoints { get; set; }
    public DbSet<DailyThroughputEntity> Throughput { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.LogTo(Console.WriteLine, LogLevel.Warning)
            .EnableDetailedErrors();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new TrialLicenseConfiguration());
        modelBuilder.ApplyConfiguration(new EndpointSettingsConfiguration());
        modelBuilder.ApplyConfiguration(new EventLogItemConfiguration());
        modelBuilder.ApplyConfiguration(new MessageRedirectsConfiguration());
        modelBuilder.ApplyConfiguration(new SubscriptionConfiguration());
        modelBuilder.ApplyConfiguration(new QueueAddressConfiguration());
        modelBuilder.ApplyConfiguration(new KnownEndpointConfiguration());
        modelBuilder.ApplyConfiguration(new CustomCheckConfiguration());
        modelBuilder.ApplyConfiguration(new RetryHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new FailedErrorImportConfiguration());
        modelBuilder.ApplyConfiguration(new ExternalIntegrationDispatchRequestConfiguration());
        modelBuilder.ApplyConfiguration(new ArchiveOperationConfiguration());
        modelBuilder.ApplyConfiguration(new FailedMessageConfiguration());
        modelBuilder.ApplyConfiguration(new RetryBatchConfiguration());
        modelBuilder.ApplyConfiguration(new FailedMessageRetryConfiguration());
        modelBuilder.ApplyConfiguration(new GroupCommentConfiguration());
        modelBuilder.ApplyConfiguration(new RetryBatchNowForwardingConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationsSettingsConfiguration());
        modelBuilder.ApplyConfiguration(new LicensingMetadataEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ThroughputEndpointConfiguration());
        modelBuilder.ApplyConfiguration(new DailyThroughputConfiguration());

        OnModelCreatingProvider(modelBuilder);
    }

    protected virtual void OnModelCreatingProvider(ModelBuilder modelBuilder)
    {
    }
}
