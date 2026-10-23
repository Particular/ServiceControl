namespace ServiceControl.Federation.Aggregation;

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

public enum ValueKind
{
    Text,
    Time,
    Duration,
    Number
}

/// <summary>
/// Reproduces a member's sort order over JSON rows so that rows from several members can be merged
/// into one correctly ordered list. Ties fall back to an ordinal comparison of a tie-breaker field;
/// the merge itself is stable, so rows that still tie keep member order (lead first).
/// </summary>
public sealed class RowOrder(string field, ValueKind kind, bool descending, string? tieBreaker = null) : IComparer<JsonNode?>
{
    public string Field { get; } = field;

    public bool Descending { get; } = descending;

    public int Compare(JsonNode? x, JsonNode? y)
    {
        var result = CompareField(x, y, Field, kind);
        if (Descending)
        {
            result = -result;
        }

        if (result == 0 && tieBreaker != null)
        {
            result = string.CompareOrdinal(JsonValues.Text(x, tieBreaker), JsonValues.Text(y, tieBreaker));
        }

        return result;
    }

    static int CompareField(JsonNode? x, JsonNode? y, string path, ValueKind kind) => kind switch
    {
        ValueKind.Time => Nullable.Compare(JsonValues.TimeTicks(x, path), JsonValues.TimeTicks(y, path)),
        ValueKind.Duration => Nullable.Compare(JsonValues.DurationTicks(x, path), JsonValues.DurationTicks(y, path)),
        ValueKind.Number => Nullable.Compare(JsonValues.Number(x, path), JsonValues.Number(y, path)),
        ValueKind.Text => string.CompareOrdinal(JsonValues.Text(x, path), JsonValues.Text(y, path)),
        _ => 0
    };

    static bool IsDescending(string? direction, bool defaultDescending) => direction switch
    {
        "asc" => false,
        "desc" => true,
        _ => defaultDescending
    };

    /// <summary>
    /// Failed message lists (<c>errors</c>, <c>endpoints/{name}/errors</c>, group errors). Mirrors
    /// <c>FailedMessageQueryFilters.Sort</c>: unknown sort tokens fall back to <c>time_sent</c>.
    /// </summary>
    public static RowOrder FailedMessages(string? sort, string? direction)
    {
        var descending = IsDescending(direction, true);
        return sort switch
        {
            "id" or "message_id" => new RowOrder("message_id", ValueKind.Text, descending, "id"),
            "message_type" => new RowOrder("message_type", ValueKind.Text, descending, "id"),
            "status" => new RowOrder("status", ValueKind.Text, descending, "id"),
            "modified" => new RowOrder("last_modified", ValueKind.Time, descending, "id"),
            "time_of_failure" => new RowOrder("time_of_failure", ValueKind.Time, descending, "id"),
            _ => new RowOrder("time_sent", ValueKind.Time, descending, "id")
        };
    }

    /// <summary>
    /// Audit and composite message lists. Mirrors <c>MessageViewComparer</c>.
    /// </summary>
    public static RowOrder Messages(string? sort, string? direction)
    {
        var descending = IsDescending(direction, true);
        return sort switch
        {
            "id" or "message_id" => new RowOrder("message_id", ValueKind.Text, descending, "id"),
            "message_type" => new RowOrder("message_type", ValueKind.Text, descending, "id"),
            "status" => new RowOrder("status", ValueKind.Text, descending, "id"),
            "processed_at" => new RowOrder("processed_at", ValueKind.Time, descending, "id"),
            "critical_time" => new RowOrder("critical_time", ValueKind.Duration, descending, "id"),
            "delivery_time" => new RowOrder("delivery_time", ValueKind.Duration, descending, "id"),
            "processing_time" => new RowOrder("processing_time", ValueKind.Duration, descending, "id"),
            _ => new RowOrder("time_sent", ValueKind.Time, descending, "id")
        };
    }

    /// <summary>
    /// Custom checks are ordered by report time, oldest first, and take no sort parameters.
    /// </summary>
    public static RowOrder CustomChecks() => new("reported_at", ValueKind.Time, false, "id");

    /// <summary>
    /// The event log is ordered newest first and takes no sort parameters.
    /// </summary>
    public static RowOrder EventLog() => new("raised_at", ValueKind.Time, true, "id");

    /// <summary>
    /// Mirrors <c>MessageRedirectsSortingExtensions.Sort</c>.
    /// </summary>
    public static RowOrder Redirects(string? sort, string? direction)
    {
        var descending = IsDescending(direction, true);
        return string.Equals(sort, "to_physical_address", StringComparison.OrdinalIgnoreCase)
            ? new RowOrder("to_physical_address", ValueKind.Text, descending, "message_redirect_id")
            : new RowOrder("from_physical_address", ValueKind.Text, descending, "message_redirect_id");
    }
}
