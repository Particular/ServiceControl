namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class KnownEndpointConfiguration : IEntityTypeConfiguration<KnownEndpointEntity>
{
    public void Configure(EntityTypeBuilder<KnownEndpointEntity> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Name).IsRequired().HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.HostId).IsRequired();
        builder.Property(e => e.Host).IsRequired().HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.Monitored).IsRequired();
    }
}
