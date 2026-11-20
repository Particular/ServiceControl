namespace ServiceControl.Persistence.EFCore.DataMigration;

using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.Entities;

/// <summary>
/// Turns a group comment from the source into the row the product's own edit would write. The writer skips a null or blank comment before it calls this.
/// </summary>
static class GroupCommentRowMapper
{
    public static GroupCommentEntity Map(GroupComment comment) => new()
    {
        GroupId = comment.Id,
        Comment = comment.Comment!
    };
}
