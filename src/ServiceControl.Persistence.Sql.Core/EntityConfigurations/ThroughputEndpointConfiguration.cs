namespace ServiceControl.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class ThroughputEndpointConfiguration : IEntityTypeConfiguration<ThroughputEndpointEntity>
{
    public void Configure(EntityTypeBuilder<ThroughputEndpointEntity> builder)
    {
        builder.ToTable("ThroughputEndpoint")
            .HasIndex(e => new
            {
                e.EndpointName,
                e.ThroughputSource
            }, "UC_ThroughputEndpoint_EndpointName_ThroughputSource")
            .IsUnique();
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EndpointName)
            .IsRequired()
            .HasMaxLength(200);
        builder.Property(e => e.ThroughputSource)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.SanitizedEndpointName);
        builder.Property(e => e.EndpointIndicators);
        builder.Property(e => e.UserIndicator);
        builder.Property(e => e.Scope);
        builder.Property(e => e.LastCollectedData);
    }
}
