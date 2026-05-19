namespace ServiceControl.Audit.Infrastructure
{
    using System.Collections.Frozen;
    using System.Collections.Generic;

    public class SortInfo
    {
        public string Direction { get; }
        public string Sort { get; }

        public SortInfo(string sort, string direction)
        {
            Sort = sort;
            Direction = direction;
        }

        // Single source of truth for the audit sort tokens the API accepts.
        // Consumed by SortInfoModelBinder so the web allowlist cannot drift
        // from the fields the audit message query actually sorts by (anything
        // not listed here falls back to TimeSent in the persistence layer).
        public static readonly FrozenSet<string> AllowedSortOptions = new HashSet<string>
        {
            "id",
            "message_id",
            "message_type",
            "status",
            "time_sent",
            "processed_at",
            "critical_time",
            "delivery_time",
            "processing_time"
        }.ToFrozenSet();
    }
}