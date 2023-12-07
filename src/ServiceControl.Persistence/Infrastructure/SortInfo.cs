namespace ServiceControl.Persistence.Infrastructure
{
    using System.Collections.Frozen;
    using System.Collections.Generic;
    using System.Diagnostics;

    [DebuggerDisplay("{Sort} {Direction}")]
    public class SortInfo(string sort, string direction = "desc")
    {
        public string Direction { get; } = direction is not "asc" and not "desc" ? "desc" : direction;
        public string Sort { get; } = !AllowableSortOptions.Contains(sort) ? "time_sent" : sort;

        static readonly FrozenSet<string> AllowableSortOptions = new HashSet<string>
        {
            "processed_at",
            "id",
            "message_type",
            "time_sent",
            "critical_time",
            "delivery_time",
            "processing_time",
            "status",
            "message_id"
        }.ToFrozenSet();
    }
}