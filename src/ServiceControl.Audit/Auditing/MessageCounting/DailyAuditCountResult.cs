namespace ServiceControl.Audit.Auditing.MessageCounting
{
    using System;
    using System.Collections.Generic;
    using ServiceControl.Audit.Auditing.MessagesView;

    class DailyAuditCountResult
    {
        public TimeSpan AuditRetention { get; set; }
        public IList<DailyAuditCount> Days { get; set; }
    }
}
