namespace ServiceControl.Audit.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class KnownEndpointInsertOnlyConfiguration : IEntityTypeConfiguration<KnownEndpointInsertOnlyEntity>
{
    public void Configure(EntityTypeBuilder<KnownEndpointInsertOnlyEntity> builder)
    {
        builder.ToTable("KnownEndpointsInsertOnly");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.KnownEndpointId).IsRequired();
        builder.Property(e => e.Name).IsRequired();
        builder.Property(e => e.HostId).IsRequired();
        builder.Property(e => e.Host).IsRequired();
        builder.Property(e => e.LastSeen).IsRequired();

        builder.HasIndex(e => e.LastSeen);
        builder.HasIndex(e => e.KnownEndpointId);
    }
}
