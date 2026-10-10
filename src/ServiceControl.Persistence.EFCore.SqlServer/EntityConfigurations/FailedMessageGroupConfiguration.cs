namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class FailedMessageGroupConfiguration : IEntityTypeConfiguration<FailedMessageGroupEntity>
{
    public void Configure(EntityTypeBuilder<FailedMessageGroupEntity> builder) =>
        builder.HasIndex(e => new { e.Type, e.GroupId })
            .IncludeProperties(nameof(FailedMessageGroupEntity.Title));
}
