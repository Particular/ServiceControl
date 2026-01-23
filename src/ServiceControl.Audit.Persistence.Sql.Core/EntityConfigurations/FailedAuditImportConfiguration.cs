namespace ServiceControl.Audit.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class FailedAuditImportConfiguration : IEntityTypeConfiguration<FailedAuditImportEntity>
{
    public void Configure(EntityTypeBuilder<FailedAuditImportEntity> builder)
    {
        builder.ToTable("FailedAuditImports");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).IsRequired();
        builder.Property(e => e.MessageJson).IsRequired();
    }
}
