namespace ServiceControl.Persistence.Infrastructure
{
    using System.Collections.Frozen;
    using System.Collections.Generic;
    using System.Diagnostics;

    [DebuggerDisplay("{Sort} {Direction}")]
    public class SortInfo(string sort = null, string direction = null)
    {
        public string Direction { get; } = string.IsNullOrWhiteSpace(direction) ? "desc" : direction;
        public string Sort { get; } = string.IsNullOrWhiteSpace(sort) ? "time_sent" : sort;

        // Single source of truth for the sort tokens the API accepts. The model
        // binder gates incoming requests against this set and the persistence
        // layer resolves each accepted token to an index field per endpoint
        // (anything it does not recognise falls back to TimeSent). It is the
        // union of the fields sortable by the message endpoints (processed_at,
        // critical_time, delivery_time, processing_time) and the failed-message
        // endpoints (time_of_failure, modified), so the web allowlist and the
        // persistence query can no longer drift apart.
        public static readonly FrozenSet<string> AllowedSortOptions = new HashSet<string>
        {
            "id",
            "message_id",
            "message_type",
            "status",
            "time_sent",
            "modified",
            "time_of_failure",
            "processed_at",
            "critical_time",
            "delivery_time",
            "processing_time"
        }.ToFrozenSet();
    }
}