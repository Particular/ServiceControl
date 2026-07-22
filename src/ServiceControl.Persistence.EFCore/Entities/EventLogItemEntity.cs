namespace ServiceControl.Persistence.EFCore.Entities;

using ServiceControl.EventLog;

public class EventLogItemEntity
{
    public long Id { get; set; }

    // The API-visible identity, assigned by EventLogMappingDefinition as
    // "EventLogItem/{Category}/{EventType}/{guid}"
    public required string EventLogItemId { get; set; }

    public required string Description { get; set; }

    public Severity Severity { get; set; }

    public DateTime RaisedAt { get; set; }

    public List<string> RelatedTo { get; set; } = [];

    public required string Category { get; set; }

    public required string EventType { get; set; }
}
