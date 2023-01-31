namespace ServiceControl.CompositeViews.MessageCounting
{
    using System;

    class DailyAuditCount
    {
        public DateTime UtcDate { get; set; }
        public EndpointAuditCount[] Data { get; set; }
    }

    class EndpointAuditCount
    {
        public string Name { get; set; }
        public long Count { get; set; }
    }
}
