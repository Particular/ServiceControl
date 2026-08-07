namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class LicensingEndpointThroughputConfiguration : IEntityTypeConfiguration<LicensingEndpointThroughputEntity>
{
    public void Configure(EntityTypeBuilder<LicensingEndpointThroughputEntity> builder)
    {
        builder.HasKey(e => new { e.NormalizedName, e.ThroughputSource, e.DateUtc });
        builder.Property(e => e.NormalizedName).HasMaxLength(ColumnLengths.LicensingEndpointNameLength);
        builder.HasOne<LicensingEndpointEntity>()
            .WithMany()
            .HasForeignKey(e => new { e.NormalizedName, e.ThroughputSource })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.DateUtc);
    }
}
