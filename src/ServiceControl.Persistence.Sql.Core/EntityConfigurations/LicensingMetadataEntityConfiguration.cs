namespace ServiceControl.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class LicensingMetadataEntityConfiguration : IEntityTypeConfiguration<LicensingMetadataEntity>
{
    public void Configure(EntityTypeBuilder<LicensingMetadataEntity> builder)
    {
        builder.ToTable("LicensingMetadata")
            .HasIndex(e => e.Key)
            .IsUnique();
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Key)
            .IsRequired()
            .HasMaxLength(200);
        builder.Property(e => e.Data)
            .IsRequired()
            .HasMaxLength(2000);
    }
}
