namespace ServiceControl.CompositeViews.MessageCounting
{
    using System;
    using System.Collections.Generic;

    class DailyAuditCountResult
    {
        public TimeSpan AuditRetention { get; set; }
        public IList<DailyAuditCount> Days { get; set; }
    }
}
