namespace ServiceControl.EventLog
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One event log item as read back and returned by the API.
    /// </summary>
    public class EventLogItemView
    {
        /// <summary>
        /// Assigned by whichever persister stored the item
        /// </summary>
        public string Id { get; set; }
        public string Description { get; set; }
        public Severity Severity { get; set; }
        public DateTime RaisedAt { get; set; }
        public List<string> RelatedTo { get; set; }
        public string Category { get; set; }
        public string EventType { get; set; }
    }
}
