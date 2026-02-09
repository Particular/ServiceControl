namespace ServiceControl.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class RetryBatchNowForwardingConfiguration : IEntityTypeConfiguration<RetryBatchNowForwardingEntity>
{
    public void Configure(EntityTypeBuilder<RetryBatchNowForwardingEntity> builder)
    {
        builder.ToTable("RetryBatchNowForwarding");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).IsRequired();
        builder.Property(e => e.RetryBatchId).HasMaxLength(200).IsRequired();

        builder.HasIndex(e => e.RetryBatchId);
    }
}
