namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class SubscriptionConfiguration : IEntityTypeConfiguration<SubscriptionEntity>
{
    public void Configure(EntityTypeBuilder<SubscriptionEntity> builder)
    {
        builder.HasKey(e => new { e.MessageType, e.TransportAddress });
        builder.Property(e => e.MessageType).IsRequired().HasMaxLength(ColumnLengths.SubscriptionKeyLength);
        builder.Property(e => e.TransportAddress).IsRequired().HasMaxLength(ColumnLengths.SubscriptionKeyLength);
        builder.Property(e => e.Endpoint).IsRequired().HasMaxLength(ColumnLengths.ShortTextLength);
    }
}
