namespace ServiceControl.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class MessageRedirectsConfiguration : IEntityTypeConfiguration<MessageRedirectsEntity>
{
    public void Configure(EntityTypeBuilder<MessageRedirectsEntity> builder)
    {
        builder.ToTable("MessageRedirects");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .IsRequired();

        builder.Property(e => e.ETag)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.LastModified)
            .IsRequired();

        builder.Property(e => e.RedirectsJson)
            .IsRequired();
    }
}
