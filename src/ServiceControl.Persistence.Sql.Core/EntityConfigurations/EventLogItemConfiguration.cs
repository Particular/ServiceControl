namespace ServiceControl.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class EventLogItemConfiguration : IEntityTypeConfiguration<EventLogItemEntity>
{
    public void Configure(EntityTypeBuilder<EventLogItemEntity> builder)
    {
        builder.ToTable("EventLogItems");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .IsRequired();

        builder.Property(e => e.Description)
            .IsRequired();

        builder.Property(e => e.Severity)
            .IsRequired();

        builder.Property(e => e.RaisedAt)
            .IsRequired();

        builder.Property(e => e.Category)
            .HasMaxLength(200);

        builder.Property(e => e.EventType)
            .HasMaxLength(200);

        builder.Property(e => e.RelatedToJson)
            .HasColumnType("jsonb")
            .HasMaxLength(4000);

        // Index for querying by RaisedAt
        builder.HasIndex(e => e.RaisedAt);
    }
}
