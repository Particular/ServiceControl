namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class EventLogItemConfiguration : IEntityTypeConfiguration<EventLogItemEntity>
{
    public void Configure(EntityTypeBuilder<EventLogItemEntity> builder)
    {
        builder.ToTable("EventLogItems");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.UniqueEventId).IsRequired();
        builder.HasIndex(e => e.UniqueEventId).IsUnique();
        builder.Property(e => e.Description).IsRequired();
        builder.Property(e => e.Severity).IsRequired();
        builder.Property(e => e.RaisedAt).IsRequired();
        // RelatedTo is only ever returned to the API, never queried, so it does not warrant a child table.
        builder.Property(e => e.RelatedTo).IsRequired();
        builder.Property(e => e.Category).IsRequired().HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.EventType).IsRequired().HasMaxLength(ColumnLengths.ShortTextLength);
        // Every read is "order by RaisedAt descending" plus paging. The key is included as a
        // tiebreaker so that items sharing a RaisedAt do not shuffle between pages, and so that
        // MAX(RaisedAt) for the ETag is an index seek.
        builder.HasIndex(e => new { e.RaisedAt, e.Id }).IsDescending();
    }
}
