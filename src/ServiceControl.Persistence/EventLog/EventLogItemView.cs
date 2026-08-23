namespace ServiceControl.EventLog
{
    using System;
    using System.Collections.Generic;
    using Persistence.Infrastructure;

    /// <summary>
    /// One event log item as read back and returned by the API.
    /// </summary>
    public class EventLogItemView : IVersionedRow
    {
        /// <summary>
        /// Assigned by whichever persister stored the item, and opaque.
        /// </summary>
        public required string Id { get; set; }
        public required string Description { get; set; }
        public Severity Severity { get; set; }
        public DateTime RaisedAt { get; set; }
        public List<string> RelatedTo { get; set; } = [];
        public required string Category { get; set; }
        public required string EventType { get; set; }
        object?[] IVersionedRow.VersionFields =>
            [Id, Description, Severity, RaisedAt, Category, EventType, .. RelatedTo];
    }
}
