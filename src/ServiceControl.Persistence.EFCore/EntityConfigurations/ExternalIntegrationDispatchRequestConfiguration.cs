namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class ExternalIntegrationDispatchRequestConfiguration : IEntityTypeConfiguration<ExternalIntegrationDispatchRequestEntity>
{
    public void Configure(EntityTypeBuilder<ExternalIntegrationDispatchRequestEntity> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.DispatchContextTypeName).IsRequired().HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.DispatchContextJson).IsRequired();
    }
}
