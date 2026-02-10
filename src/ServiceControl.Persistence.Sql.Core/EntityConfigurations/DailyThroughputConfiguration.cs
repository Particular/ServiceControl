namespace ServiceControl.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class DailyThroughputConfiguration : IEntityTypeConfiguration<DailyThroughputEntity>
{
    public void Configure(EntityTypeBuilder<DailyThroughputEntity> builder)
    {
        builder.ToTable("DailyThroughput")
            .HasIndex(e => new
            {
                e.EndpointName,
                e.ThroughputSource,
                e.Date
            }, "UC_DailyThroughput_EndpointName_ThroughputSource_Date")
            .IsUnique();
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EndpointName)
            .IsRequired()
            .HasMaxLength(200);
        builder.Property(e => e.ThroughputSource)
            .IsRequired()
            .HasMaxLength(50);
        builder.Property(e => e.Date)
            .IsRequired();
        builder.Property(e => e.MessageCount)
            .IsRequired();
    }
}
