namespace ServiceControl.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class MessageBodyConfiguration : IEntityTypeConfiguration<MessageBodyEntity>
{
    public void Configure(EntityTypeBuilder<MessageBodyEntity> builder)
    {
        builder.ToTable("MessageBodies");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).IsRequired();
        builder.Property(e => e.Body).IsRequired();
        builder.Property(e => e.ContentType).HasMaxLength(200).IsRequired();
        builder.Property(e => e.BodySize).IsRequired();
        builder.Property(e => e.Etag).HasMaxLength(100);
    }
}
