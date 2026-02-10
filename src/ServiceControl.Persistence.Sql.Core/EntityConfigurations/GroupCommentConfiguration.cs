namespace ServiceControl.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class GroupCommentConfiguration : IEntityTypeConfiguration<GroupCommentEntity>
{
    public void Configure(EntityTypeBuilder<GroupCommentEntity> builder)
    {
        builder.ToTable("GroupComments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).IsRequired();
        builder.Property(e => e.GroupId).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Comment).IsRequired();

        builder.HasIndex(e => e.GroupId);
    }
}
