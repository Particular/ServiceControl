namespace ServiceControl.Persistence.EFCore.Entities;

using ServiceControl.EventLog;

public class EventLogItemEntity
{
    // The physical key only. Sequential so inserts stay at the tail of the index, and narrow so it
    // is a cheap tiebreaker for RaisedAt paging.
    public long Id { get; set; }

    // A globally unique identity common between persisters (e.g. used in migrations).
    public Guid UniqueEventId { get; set; }

    public required string Description { get; set; }

    public Severity Severity { get; set; }

    public DateTime RaisedAt { get; set; }

    public List<string> RelatedTo { get; set; } = [];

    public required string Category { get; set; }

    public required string EventType { get; set; }
}
