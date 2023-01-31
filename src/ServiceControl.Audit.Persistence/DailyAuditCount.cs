namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System;

    public class DailyAuditCount
    {
        public DateTime UtcDate { get; set; }
        public EndpointAuditCount[] Data { get; set; }
    }

    public class EndpointAuditCount
    {
        public string Name { get; set; }
        public long Count { get; set; }
    }
}
