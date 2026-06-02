#nullable enable

namespace ServiceControl.Infrastructure.Mcp;

using System.Collections.Generic;

public class McpErrorsSummaryResult
{
    public long Unresolved { get; init; }
    public long Archived { get; init; }
    public long Resolved { get; init; }
    public long RetryIssued { get; init; }

    public static McpErrorsSummaryResult From(IDictionary<string, object> summary)
    {
        return new McpErrorsSummaryResult
        {
            Unresolved = GetCount(summary, "unresolved"),
            Archived = GetCount(summary, "archived"),
            Resolved = GetCount(summary, "resolved"),
            RetryIssued = GetCount(summary, "retryissued")
        };
    }

    static long GetCount(IDictionary<string, object> summary, string key)
    {
        return summary.TryGetValue(key, out var value)
            ? System.Convert.ToInt64(value)
            : 0L;
    }
}
