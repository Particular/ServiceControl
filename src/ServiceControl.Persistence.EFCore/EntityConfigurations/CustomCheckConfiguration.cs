namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class CustomCheckConfiguration : IEntityTypeConfiguration<CustomCheckEntity>
{
    public void Configure(EntityTypeBuilder<CustomCheckEntity> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.Property(e => e.CustomCheckId).IsRequired();
        builder.Property(e => e.Category).IsRequired();
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.ReportedAt).IsRequired();
        builder.Property(e => e.FailureReason);
        builder.Property(e => e.OriginatingEndpointName).IsRequired();
        builder.Property(e => e.OriginatingEndpointHostId).IsRequired();
        builder.Property(e => e.OriginatingEndpointHost).IsRequired();
    }
}