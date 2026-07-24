namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class EndpointSettingsConfiguration : IEntityTypeConfiguration<EndpointSettingsEntity>
{
    public void Configure(EntityTypeBuilder<EndpointSettingsEntity> builder)
    {
        builder.ToTable("EndpointSettings");
        builder.HasKey(x => x.Name);
        builder.Property(x => x.Name).HasMaxLength(ColumnLengths.ShortTextLength).IsRequired();
    }
}