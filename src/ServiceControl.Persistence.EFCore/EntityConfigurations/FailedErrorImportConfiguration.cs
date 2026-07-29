namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class FailedErrorImportConfiguration : IEntityTypeConfiguration<FailedErrorImportEntity>
{
    public void Configure(EntityTypeBuilder<FailedErrorImportEntity> builder)
    {
        builder.HasKey(e => e.UniqueMessageId);
        builder.Property(e => e.UniqueMessageId).ValueGeneratedNever();

        builder.Property(e => e.FailedAt).IsRequired();
        builder.Property(e => e.MessageId).IsRequired().HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.HeadersJson).IsRequired();
        builder.Property(e => e.Body).IsRequired();
        builder.Property(e => e.BodyStoredExternally).IsRequired();
        builder.Property(e => e.ExceptionInfo).IsRequired();

        builder.HasIndex(e => e.FailedAt);
    }
}
