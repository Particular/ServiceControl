namespace ServiceControl.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class CustomCheckConfiguration : IEntityTypeConfiguration<CustomCheckEntity>
{
    public void Configure(EntityTypeBuilder<CustomCheckEntity> builder)
    {
        builder.ToTable("CustomChecks");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CustomCheckId).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Category).HasMaxLength(500);
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.ReportedAt).IsRequired();
        builder.Property(e => e.FailureReason);
        builder.Property(e => e.EndpointName).IsRequired().HasMaxLength(500);
        builder.Property(e => e.HostId).IsRequired();
        builder.Property(e => e.Host).IsRequired().HasMaxLength(500);

        // Index for filtering by status
        builder.HasIndex(e => e.Status);
    }
}
