namespace ServiceControl.Persistence.Sql.Core.Entities;

using System;

public class EventLogItemEntity
{
    public Guid Id { get; set; }
    public required string Description { get; set; }
    public int Severity { get; set; }
    public DateTime RaisedAt { get; set; }
    public string? RelatedTo { get; set; } // Stored as JSON array
    public string? Category { get; set; }
    public string? EventType { get; set; }
}
