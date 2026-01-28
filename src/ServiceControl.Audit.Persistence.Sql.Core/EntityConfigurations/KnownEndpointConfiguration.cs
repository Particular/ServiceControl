namespace ServiceControl.Audit.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class KnownEndpointConfiguration : IEntityTypeConfiguration<KnownEndpointEntity>
{
    public void Configure(EntityTypeBuilder<KnownEndpointEntity> builder)
    {
        builder.ToTable("KnownEndpoints");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id);
        builder.Property(e => e.Name).IsRequired();
        builder.Property(e => e.HostId).IsRequired();
        builder.Property(e => e.Host).IsRequired();
        builder.Property(e => e.LastSeen).IsRequired();

        builder.HasIndex(e => e.LastSeen);
    }
}
