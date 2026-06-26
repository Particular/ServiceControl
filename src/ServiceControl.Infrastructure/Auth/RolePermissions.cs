#nullable enable
namespace ServiceControl.Infrastructure.Auth;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Role → permission policy. Two roles:
/// <list type="bullet">
///   <item><c>reader</c> — granted every <c>*:*:view</c> permission (read-only access).</item>
///   <item><c>writer</c> — granted every permission (<c>*:*:*</c>).</item>
/// </list>
/// The wildcard patterns (<c>*</c> is a colon-segment wildcard) are the source of truth, but they are
/// <b>expanded once</b> at type initialization against <see cref="Permissions.All"/> into a concrete,
/// immutable <see cref="FrozenSet{T}"/> of granted permissions per role. As a result both
/// <see cref="IsGranted"/> and <see cref="GetPermissions(string)"/> are O(1) hash lookups with no
/// per-call pattern matching or allocation.
/// </summary>
public static class RolePermissions
{
    /// <summary>Read-only role: every <c>*:*:view</c> permission.</summary>
    public const string Reader = "reader";

    /// <summary>Full-access role: every permission.</summary>
    public const string Writer = "writer";

    /// <summary>
    /// Platform-administrator role: read-only on everything, plus full management of the configuration /
    /// admin-area resources (licensing, notifications, retry redirects, throughput, connections) — but
    /// <b>not</b> the message-triage write actions (retry/edit/archive/restore).
    /// </summary>
    public const string Admin = "admin";

    // Source of truth: the wildcard pattern(s) each role grants.
    static readonly Dictionary<string, string[]> RolePatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        [Reader] = ["*:*:view"],
        [Writer] = ["*:*:*"],
        [Admin] =
        [
            "*:*:view",
            "error:licensing:*",
            "error:notifications:*",
            "error:redirects:*",
            "error:throughput:*",
            "error:connections:*",
        ],
    };

    // Expanded once against the full permission catalogue: role -> concrete granted permissions.
    static readonly FrozenDictionary<string, FrozenSet<string>> PermissionsByRole = Expand();

    /// <summary>
    /// Returns <see langword="true"/> if any of the supplied <paramref name="roles"/> grants the
    /// requested <paramref name="permission"/>. O(1) per role — a frozen-set membership test.
    /// </summary>
    public static bool IsGranted(IEnumerable<string> roles, string permission)
    {
        foreach (var role in roles)
        {
            if (PermissionsByRole.TryGetValue(role, out var granted) && granted.Contains(permission))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The complete set of permissions granted to a single role (empty if the role is unknown).
    /// O(1) and allocation-free — returns the precomputed frozen set.
    /// </summary>
    public static IReadOnlySet<string> GetPermissions(string role) =>
        PermissionsByRole.TryGetValue(role, out var granted) ? granted : FrozenSet<string>.Empty;

    /// <summary>
    /// The union of permissions granted across several <paramref name="roles"/>. Allocation-free for the
    /// common single-role case; only the multi-role union allocates.
    /// </summary>
    public static IReadOnlySet<string> GetPermissions(IEnumerable<string> roles)
    {
        var list = roles as IReadOnlyList<string> ?? roles.ToList();
        if (list.Count <= 1)
        {
            return list.Count == 0 ? FrozenSet<string>.Empty : GetPermissions(list[0]);
        }

        var union = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in list)
        {
            if (PermissionsByRole.TryGetValue(role, out var granted))
            {
                union.UnionWith(granted);
            }
        }

        return union;
    }

    static FrozenDictionary<string, FrozenSet<string>> Expand()
    {
        var expanded = new Dictionary<string, FrozenSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (role, patterns) in RolePatterns)
        {
            expanded[role] = Permissions.All
                .Where(permission => patterns.Any(pattern => Matches(pattern, permission)))
                .ToFrozenSet(StringComparer.Ordinal);
        }

        return expanded.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Matches a colon-delimited permission against a pattern where <c>*</c> is a segment wildcard.</summary>
    static bool Matches(string pattern, string permission)
    {
        var patternSegments = pattern.Split(':');
        var permissionSegments = permission.Split(':');

        if (patternSegments.Length != permissionSegments.Length)
        {
            return false;
        }

        for (var i = 0; i < patternSegments.Length; i++)
        {
            if (patternSegments[i] != "*"
                && !string.Equals(patternSegments[i], permissionSegments[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
