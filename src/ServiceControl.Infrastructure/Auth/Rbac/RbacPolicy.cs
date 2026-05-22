#nullable enable
namespace ServiceControl.Infrastructure.Auth.Rbac;

using System.Collections.Generic;

/// <summary>
/// The top-level RBAC policy, loaded from rbac.yaml.
/// </summary>
public sealed record RbacPolicy(int SchemaVersion, IReadOnlyDictionary<string, RbacRole> Roles);

/// <summary>
/// A named role with IdP bindings and permission grants.
/// </summary>
public sealed record RbacRole(string Name, IReadOnlyList<string> Bindings, IReadOnlyList<PermissionGrant> Permissions);

/// <summary>
/// A single permission grant, optionally scoped to a resource pattern set.
/// A null Scope means the permission applies to all resources (unrestricted).
/// </summary>
public sealed record PermissionGrant(string Permission, ResourceScopeSpec? Scope);

/// <summary>
/// Allowlist and denylist patterns for resource-scoped permissions.
/// Deny wins if a resource matches both allow and deny patterns.
/// </summary>
public sealed record ResourceScopeSpec(IReadOnlyList<string> Allow, IReadOnlyList<string> Deny);
