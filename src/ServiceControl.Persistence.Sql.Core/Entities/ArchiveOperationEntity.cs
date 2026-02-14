namespace ServiceControl.Persistence.Sql.Core.Entities;

using System;

public class ArchiveOperationEntity
{
    public Guid Id { get; set; }
    public string RequestId { get; set; } = null!;
    public string GroupName { get; set; } = null!;
    public int ArchiveType { get; set; }  // ArchiveType enum as int
    public int ArchiveState { get; set; }  // ArchiveState enum as int
    public int TotalNumberOfMessages { get; set; }
    public int NumberOfMessagesArchived { get; set; }
    public int NumberOfBatches { get; set; }
    public int CurrentBatch { get; set; }
    public DateTime Started { get; set; }
    public DateTime? Last { get; set; }
    public DateTime? CompletionTime { get; set; }
}
