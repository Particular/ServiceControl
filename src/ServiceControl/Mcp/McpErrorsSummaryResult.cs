#nullable enable
namespace ServiceControl.Mcp;

public sealed class McpErrorsSummaryResult
{
    public long Unresolved { get; init; }
    public long Archived { get; init; }
    public long Resolved { get; init; }
    public long RetryIssued { get; init; }

    public static McpErrorsSummaryResult From(long unresolved, long archived, long resolved, long retryIssued)
        => new()
        {
            Unresolved = unresolved,
            Archived = archived,
            Resolved = resolved,
            RetryIssued = retryIssued
        };
}