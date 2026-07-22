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
        // Long enough for "EventLogItem/{Category}/{EventType}/{guid}" form. Deliberately not
        // indexed and not unique. Nothing looks an item up by it, and each id embeds a fresh Guid.
        builder.Property(e => e.EventLogItemId).IsRequired().HasMaxLength(600);
        builder.Property(e => e.Description).IsRequired();
        builder.Property(e => e.Severity).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.RaisedAt).IsRequired();
        // RelatedTo is only ever returned to the API, never queried, so it does not warrant a child table.
        builder.Property(e => e.RelatedTo).IsRequired();
        builder.Property(e => e.Category).IsRequired().HasMaxLength(255);
        builder.Property(e => e.EventType).IsRequired().HasMaxLength(255);
        // Every read is "order by RaisedAt descending" plus paging. The key is included as a
        // tiebreaker so that items sharing a RaisedAt do not shuffle between pages.
        builder.HasIndex(e => new { e.RaisedAt, e.Id }).IsDescending();
    }
}
