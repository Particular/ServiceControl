using System;

namespace Issue558Detector
{
    public class EventLogItem
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public int Severity { get; set; }
        public DateTime RaisedAt { get; set; }
        public string[] RelatedTo { get; set; }
        public string Category { get; set; }
        public string EventType { get; set; }
    }
}