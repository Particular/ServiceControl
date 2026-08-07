namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class LicensingEndpointConfiguration : IEntityTypeConfiguration<LicensingEndpointEntity>
{
    public void Configure(EntityTypeBuilder<LicensingEndpointEntity> builder)
    {
        builder.HasKey(e => new { e.NormalizedName, e.ThroughputSource });
        builder.Property(e => e.NormalizedName).HasMaxLength(ColumnLengths.LicensingEndpointNameLength);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(ColumnLengths.LicensingEndpointNameLength);
        builder.Property(e => e.SanitizedName).IsRequired().HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.NormalizedSanitizedName).IsRequired().HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.UserIndicator).IsRequired().HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.Scope).HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.EndpointIndicators).IsRequired();

        builder.HasIndex(e => e.NormalizedSanitizedName);
    }
}
