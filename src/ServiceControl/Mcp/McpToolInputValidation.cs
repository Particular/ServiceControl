#nullable enable
namespace ServiceControl.Mcp;

using System;
using System.Collections.Generic;

static class McpToolInputValidation
{
    static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "unresolved",
        "archived",
        "retryissued",
        "resolved"
    };

    static readonly HashSet<string> AllowedSorts = new(StringComparer.OrdinalIgnoreCase)
    {
        "time_sent",
        "message_type",
        "time_of_failure"
    };

    static readonly HashSet<string> AllowedDirections = new(StringComparer.OrdinalIgnoreCase)
    {
        "asc",
        "desc"
    };

    public static string? NormalizeOptionalFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value.Trim(), "undefined", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value.Trim();
    }

    public static string? NormalizeStatus(string? status)
    {
        var normalized = NormalizeOptionalFilter(status);
        if (normalized == null)
        {
            return null;
        }

        if (!AllowedStatuses.Contains(normalized))
        {
            throw new ArgumentException($"Unsupported status '{status}'. Supported values are unresolved, archived, retryissued, and resolved.");
        }

        return normalized.ToLowerInvariant();
    }

    public static string NormalizeSort(string? sort)
    {
        var normalized = NormalizeOptionalFilter(sort);
        if (normalized == null)
        {
            return "time_of_failure";
        }

        if (!AllowedSorts.Contains(normalized))
        {
            throw new ArgumentException($"Unsupported sort '{sort}'. Supported values are time_sent, message_type, and time_of_failure.");
        }

        return normalized.ToLowerInvariant();
    }

    public static string NormalizeDirection(string? direction)
    {
        var normalized = NormalizeOptionalFilter(direction);
        if (normalized == null)
        {
            return "desc";
        }

        if (!AllowedDirections.Contains(normalized))
        {
            throw new ArgumentException($"Unsupported direction '{direction}'. Supported values are asc and desc.");
        }

        return normalized.ToLowerInvariant();
    }
}