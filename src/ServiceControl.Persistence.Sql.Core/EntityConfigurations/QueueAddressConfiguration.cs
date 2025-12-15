namespace ServiceControl.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class QueueAddressConfiguration : IEntityTypeConfiguration<QueueAddressEntity>
{
    public void Configure(EntityTypeBuilder<QueueAddressEntity> builder)
    {
        builder.ToTable("QueueAddresses");
        builder.HasKey(e => e.PhysicalAddress);
        builder.Property(e => e.PhysicalAddress).HasMaxLength(500);
        builder.Property(e => e.FailedMessageCount).IsRequired();
    }
}
