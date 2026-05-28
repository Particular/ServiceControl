#nullable enable
namespace ServiceControl.Infrastructure.Auth.Rbac;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Evaluates whether a resource identifier is permitted under a set of allow/deny glob patterns.
/// Patterns support:
/// - Exact match: "acme.sales"
/// - Prefix wildcard: "acme.sales.*" matches "acme.sales.orders" but not "acme.sales" itself
/// - Universal wildcard: "*" matches everything
/// Deny wins: a resource matching both allow and deny is denied.
/// </summary>
public sealed class ResourceScope(IReadOnlyList<string> allow, IReadOnlyList<string> deny)
{
    /// <summary>
    /// A scope that permits all resources (allow: ["*"], deny: []).
    /// </summary>
    public static readonly ResourceScope Unrestricted = new(["*"], []);

    /// <summary>The allow-list patterns.</summary>
    public IReadOnlyList<string> Allow { get; } = allow;

    /// <summary>The deny-list patterns.</summary>
    public IReadOnlyList<string> Deny { get; } = deny;

    /// <summary>
    /// Returns true if the resource is matched by at least one allow pattern
    /// and not matched by any deny pattern.
    /// </summary>
    public bool Permits(string resource) =>
        Allow.Any(p => Matches(p, resource)) && !Deny.Any(p => Matches(p, resource));

    static bool Matches(string pattern, string resource)
    {
        // Queue addresses are stored lower-case in the index; normalise the pattern
        // to lower-case so that mixed-case policy entries (e.g. "Finance.*") work
        // correctly against lower-case queue addresses.
        var lower = pattern.ToLowerInvariant();
        return lower == "*" ||
               lower == resource ||
               (lower.EndsWith(".*", StringComparison.Ordinal) &&
                resource.StartsWith(lower[..^1], StringComparison.Ordinal));
    }
}
