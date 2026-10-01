namespace ServiceControl.Audit.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class FailedAuditImportConfiguration : IEntityTypeConfiguration<FailedAuditImportEntity>
{
    public void Configure(EntityTypeBuilder<FailedAuditImportEntity> builder)
    {
        builder.HasKey(e => e.UniqueMessageId);
        builder.Property(e => e.UniqueMessageId).ValueGeneratedNever();
        builder.Property(e => e.FailedAt).IsRequired();
        builder.Property(e => e.HeadersJson).IsRequired();
        builder.Property(e => e.Body).IsRequired();

        builder.HasIndex(e => new { e.FailedAt, e.UniqueMessageId });
    }
}
