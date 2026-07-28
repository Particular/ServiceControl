namespace ServiceControl.EventLog
{
    using System;
    using System.Collections.Generic;

    public class EventLogItem
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public Severity Severity { get; set; }
        public DateTime RaisedAt { get; set; }
        /// <summary>
        /// This could be the Id of a related document, such as the FailedMessage event, which will have more information regarding this alert.
        /// </summary>
        public List<string> RelatedTo { get; set; }
        public string Category { get; set; }
        public string EventType { get; set; }
    }
}