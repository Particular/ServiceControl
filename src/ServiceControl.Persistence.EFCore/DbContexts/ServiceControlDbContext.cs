namespace ServiceControl.Persistence.EFCore.DbContexts;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.EntityConfigurations;

public abstract class ServiceControlDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<EndpointSettingsEntity> EndpointSettings { get; set; }
    public DbSet<KnownEndpointEntity> KnownEndpoints { get; set; }
    public DbSet<FailedMessageEntity> FailedMessages { get; set; }
    public DbSet<FailedMessageGroupEntity> FailedMessageGroups { get; set; }
    public DbSet<FailedMessageRetryEntity> FailedMessageRetries { get; set; }
    public DbSet<TrialMetadataEntity> TrialMetadata { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.EnableDetailedErrors();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new EndpointSettingsConfiguration());
        modelBuilder.ApplyConfiguration(new FailedMessageConfiguration());
        modelBuilder.ApplyConfiguration(new FailedMessageGroupConfiguration());
        modelBuilder.ApplyConfiguration(new FailedMessageRetryConfiguration());
        modelBuilder.ApplyConfiguration(new KnownEndpointConfiguration());
        modelBuilder.ApplyConfiguration(new TrialMetadataConfiguration());
    }
}
