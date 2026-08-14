namespace ServiceControl.EventLog
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One event log item as written. Carries no identity: each persister assigns that itself and
    /// surfaces it on <see cref="EventLogItemView"/> when the item is read back.
    /// </summary>
    public class EventLogItem
    {
        public string? Description { get; set; }
        public Severity Severity { get; set; }
        public DateTime RaisedAt { get; set; }
        /// <summary>
        /// This could be the Id of a related document, such as the FailedMessage event, which will have more information regarding this alert.
        /// </summary>
        public List<string> RelatedTo { get; set; } = [];
        public required string Category { get; set; }
        public required string EventType { get; set; }
    }
}