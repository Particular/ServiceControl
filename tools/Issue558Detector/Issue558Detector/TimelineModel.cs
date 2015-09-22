using System;

namespace Issue558Detector
{
    public class TimelineEntry
    {
        public string Id { get; set; }
        public DateTime When { get; set; }
        public string Event { get; set; }
    }

    public enum EventClassification
    {
        Unknown,
        Ok,
        NotOk
    }

    public class ClassifiedTimelineEntry
    {
        public TimelineEntry Entry { get; set; }
        public EventClassification Classification { get; set; }
    }
}