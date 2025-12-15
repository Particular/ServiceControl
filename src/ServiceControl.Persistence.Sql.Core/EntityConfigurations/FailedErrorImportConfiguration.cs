namespace ServiceControl.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class FailedErrorImportConfiguration : IEntityTypeConfiguration<FailedErrorImportEntity>
{
    public void Configure(EntityTypeBuilder<FailedErrorImportEntity> builder)
    {
        builder.ToTable("FailedErrorImports");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).IsRequired();
        builder.Property(e => e.MessageJson).IsRequired();
        builder.Property(e => e.ExceptionInfo);
    }
}
