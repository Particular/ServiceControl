namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class MessageRedirectConfiguration : IEntityTypeConfiguration<MessageRedirectEntity>
{
    public void Configure(EntityTypeBuilder<MessageRedirectEntity> builder)
    {
        builder.HasKey(e => e.FromPhysicalAddress);

        builder.Property(e => e.FromPhysicalAddress).HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.ToPhysicalAddress).IsRequired().HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.LastModified).IsRequired();
    }
}
