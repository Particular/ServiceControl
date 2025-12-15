namespace ServiceControl.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class NotificationsSettingsConfiguration : IEntityTypeConfiguration<NotificationsSettingsEntity>
{
    public void Configure(EntityTypeBuilder<NotificationsSettingsEntity> builder)
    {
        builder.ToTable("NotificationsSettings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).IsRequired();
        builder.Property(e => e.EmailSettingsJson).IsRequired();
    }
}
