namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class RetryBatchNowForwardingConfiguration : IEntityTypeConfiguration<RetryBatchNowForwardingEntity>
{
    public void Configure(EntityTypeBuilder<RetryBatchNowForwardingEntity> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.RetryBatchId).IsRequired();
    }
}
