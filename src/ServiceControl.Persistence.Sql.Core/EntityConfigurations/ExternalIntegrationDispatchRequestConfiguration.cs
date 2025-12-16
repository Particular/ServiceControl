namespace ServiceControl.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class ExternalIntegrationDispatchRequestConfiguration : IEntityTypeConfiguration<ExternalIntegrationDispatchRequestEntity>
{
    public void Configure(EntityTypeBuilder<ExternalIntegrationDispatchRequestEntity> builder)
    {
        builder.ToTable("ExternalIntegrationDispatchRequests");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd()
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(e => e.DispatchContextJson).HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasIndex(e => e.CreatedAt);
    }
}
