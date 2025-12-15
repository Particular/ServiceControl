namespace ServiceControl.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class SubscriptionConfiguration : IEntityTypeConfiguration<SubscriptionEntity>
{
    public void Configure(EntityTypeBuilder<SubscriptionEntity> builder)
    {
        builder.ToTable("Subscriptions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(100);
        builder.Property(e => e.MessageTypeTypeName).IsRequired().HasMaxLength(500);
        builder.Property(e => e.MessageTypeVersion).IsRequired();
        builder.Property(e => e.SubscribersJson).IsRequired();

        // Unique composite index to enforce one subscription per message type/version
        builder.HasIndex(e => new { e.MessageTypeTypeName, e.MessageTypeVersion }).IsUnique();
    }
}
