namespace ServiceControl.Audit.Persistence.EFCore.PostgreSql.EntityConfigurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceControl.Audit.Persistence.EFCore.Entities;

class AuditMessageConfiguration : IEntityTypeConfiguration<AuditMessageEntity>
{
    public void Configure(EntityTypeBuilder<AuditMessageEntity> builder)
    {
        // Serves the exact MessageId arm of the full-text search predicate without a sequential scan.
        builder.HasIndex(e => e.MessageId);
    }
}
