#nullable enable
namespace ServiceControl.Infrastructure.Auth.Rbac;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

/// <summary>
/// Evaluates a user's permissions against an <see cref="RbacPolicy"/> loaded via a factory delegate.
/// The factory is called on each evaluation, allowing the policy to be reloaded at runtime.
/// <para>
/// Binding resolution rules:
/// - <c>role:X</c> matches a <c>role</c> claim with value <c>X</c>
/// - <c>group:/path</c> matches a <c>group</c> claim with value <c>/path</c>
/// </para>
/// </summary>
public sealed class PermissionEvaluator(Func<RbacPolicy> policyFactory) : IPermissionEvaluator
{
    public bool HasPermission(ClaimsPrincipal user, string permission)
    {
        var policy = policyFactory();
        foreach (var role in MatchingRoles(user, policy))
        {
            foreach (var grant in role.Permissions)
            {
                if (GrantMatchesPermission(grant, permission))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public bool IsInScope(ClaimsPrincipal user, string permission, string resource)
    {
        var policy = policyFactory();
        foreach (var role in MatchingRoles(user, policy))
        {
            foreach (var grant in role.Permissions)
            {
                if (!GrantMatchesPermission(grant, permission))
                {
                    continue;
                }

                // No scope (including wildcard "*") means the permission applies to all resources
                if (grant.Scope == null)
                {
                    return true;
                }

                var scope = new ResourceScope(grant.Scope.Allow, grant.Scope.Deny);
                if (scope.Permits(resource))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Resolves the full set of effective permissions for the user based on their claims
    /// and the current RBAC policy. Identical grants (same permission AND same scope) from
    /// multiple matching roles are deduplicated — a user in two roles that both grant
    /// <c>messages:view</c> with no scope yields exactly one entry.
    /// <para>
    /// <b>OR semantics:</b> multiple entries for the same permission with <em>different</em> scopes
    /// are all preserved. ServicePulse evaluates <c>can(permission, resource)</c> as
    /// <c>true</c> if <em>any</em> entry for that permission permits the resource.
    /// All downstream branches consuming this descriptor must apply the same OR semantics.
    /// </para>
    /// </summary>
    public EffectivePermissions Resolve(ClaimsPrincipal user)
    {
        var policy = policyFactory();
        var seen = new HashSet<string>();
        var grants = new List<EffectiveGrant>();

        foreach (var role in MatchingRoles(user, policy))
        {
            foreach (var grant in role.Permissions)
            {
                ResourceScope? scope = grant.Scope != null
                    ? new ResourceScope(grant.Scope.Allow, grant.Scope.Deny)
                    : null;

                // Build a canonical key to detect identical grants (same permission + same scope).
                // Different scopes for the same permission are intentionally preserved
                // (OR semantics: any matching entry grants access).
                var key = GrantKey(grant.Permission, scope);
                if (!seen.Add(key))
                {
                    continue;
                }

                grants.Add(new EffectiveGrant(grant.Permission, scope));
            }
        }

        return new EffectivePermissions(grants);
    }

    /// <summary>
    /// Produces a stable string key representing a (permission, scope) pair for deduplication.
    /// Scope patterns are sorted so that identical pattern sets in different order compare equal.
    /// </summary>
    static string GrantKey(string permission, ResourceScope? scope)
    {
        if (scope == null)
        {
            return permission;
        }

        var allow = string.Join(',', scope.Allow.Order(StringComparer.Ordinal));
        var deny = string.Join(',', scope.Deny.Order(StringComparer.Ordinal));
        return $"{permission}|allow:{allow}|deny:{deny}";
    }

    /// <summary>
    /// Yields all roles from the policy whose bindings match at least one of the user's claims.
    /// </summary>
    static IEnumerable<RbacRole> MatchingRoles(ClaimsPrincipal user, RbacPolicy policy)
    {
        foreach (var role in policy.Roles.Values)
        {
            if (RoleBindingsMatch(user, role.Bindings))
            {
                yield return role;
            }
        }
    }

    /// <summary>
    /// Returns true if the user has at least one claim matching any binding in the list.
    /// Binding format:
    /// - <c>role:X</c>   → claim type <c>role</c>,  value <c>X</c>
    /// - <c>group:/path</c> → claim type <c>group</c>, value <c>/path</c>
    /// </summary>
    static bool RoleBindingsMatch(ClaimsPrincipal user, IReadOnlyList<string> bindings)
    {
        foreach (var binding in bindings)
        {
            var colonIndex = binding.IndexOf(':');
            if (colonIndex < 0)
            {
                continue;
            }

            var claimType = binding[..colonIndex];
            var claimValue = binding[(colonIndex + 1)..];

            if (user.HasClaim(claimType, claimValue))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns true if the grant covers the requested permission.
    /// A <c>*</c> permission in the grant matches everything.
    /// </summary>
    static bool GrantMatchesPermission(PermissionGrant grant, string permission) =>
        grant.Permission == "*" || string.Equals(grant.Permission, permission, StringComparison.Ordinal);
}
