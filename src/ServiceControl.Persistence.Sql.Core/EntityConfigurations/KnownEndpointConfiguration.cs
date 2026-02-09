namespace ServiceControl.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class KnownEndpointConfiguration : IEntityTypeConfiguration<KnownEndpointEntity>
{
    public void Configure(EntityTypeBuilder<KnownEndpointEntity> builder)
    {
        builder.ToTable("KnownEndpoints");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EndpointName).IsRequired().HasMaxLength(500);
        builder.Property(e => e.HostId).IsRequired();
        builder.Property(e => e.Host).IsRequired().HasMaxLength(500);
        builder.Property(e => e.HostDisplayName).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Monitored).IsRequired();
    }
}
