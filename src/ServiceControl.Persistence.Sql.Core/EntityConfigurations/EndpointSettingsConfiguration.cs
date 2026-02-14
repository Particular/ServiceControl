namespace ServiceControl.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class EndpointSettingsConfiguration : IEntityTypeConfiguration<EndpointSettingsEntity>
{
    public void Configure(EntityTypeBuilder<EndpointSettingsEntity> builder)
    {
        builder.ToTable("EndpointSettings");

        builder.HasKey(e => e.Name);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.TrackInstances)
            .IsRequired();
    }
}
