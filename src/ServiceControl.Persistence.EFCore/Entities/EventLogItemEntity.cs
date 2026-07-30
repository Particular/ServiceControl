namespace ServiceControl.Persistence.EFCore.Entities;

using ServiceControl.EventLog;

public class EventLogItemEntity
{
    // Sequential so inserts stay at the tail of the index, and narrow so it is a cheap tiebreaker
    // for RaisedAt paging. Also what the API returns as EventLogItemView.Id, so it is store-local.
    public long Id { get; set; }

    public required string Description { get; set; }

    public Severity Severity { get; set; }

    public DateTime RaisedAt { get; set; }

    public List<string> RelatedTo { get; set; } = [];

    public required string Category { get; set; }

    public required string EventType { get; set; }
}
