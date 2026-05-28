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
        // Normalise both pattern and resource to lower-case.
        // Queue addresses in the RavenDB index are stored lower-case; policy entries
        // may be mixed-case (e.g. "Finance.*"), and in-memory resource identifiers
        // (from FailedMessage.QueueAddress) may also be mixed-case.
        // Lowercasing both sides ensures consistent case-insensitive comparison.
        var lowerPattern = pattern.ToLowerInvariant();
        var lowerResource = resource.ToLowerInvariant();
        return lowerPattern == "*" ||
               lowerPattern == lowerResource ||
               (lowerPattern.EndsWith(".*", StringComparison.Ordinal) &&
                lowerResource.StartsWith(lowerPattern[..^1], StringComparison.Ordinal));
    }
}
