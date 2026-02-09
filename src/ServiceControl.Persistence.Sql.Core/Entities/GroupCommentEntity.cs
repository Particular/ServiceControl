namespace ServiceControl.Persistence.Sql.Core.Entities;

using System;

public class GroupCommentEntity
{
    public Guid Id { get; set; }
    public string GroupId { get; set; } = null!;
    public string Comment { get; set; } = null!;
}
