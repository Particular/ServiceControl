namespace ServiceControl.Persistence.EFCore.Entities;

public class GroupCommentEntity
{
    public required string GroupId { get; set; }

    public required string Comment { get; set; }
}
