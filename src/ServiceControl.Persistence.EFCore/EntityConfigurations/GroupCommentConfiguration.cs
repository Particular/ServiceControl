namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class GroupCommentConfiguration : IEntityTypeConfiguration<GroupCommentEntity>
{
    public void Configure(EntityTypeBuilder<GroupCommentEntity> builder)
    {
        builder.HasKey(e => e.GroupId);

        builder.Property(e => e.GroupId).HasMaxLength(ColumnLengths.GroupIdLength).ValueGeneratedNever();
        builder.Property(e => e.Comment).IsRequired();
    }
}
