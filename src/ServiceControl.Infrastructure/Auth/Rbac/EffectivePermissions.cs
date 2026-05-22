#nullable enable
namespace ServiceControl.Infrastructure.Auth.Rbac;

using System.Collections.Generic;

/// <summary>
/// The resolved set of permissions for a user, derived from their claims and the RBAC policy.
/// </summary>
public sealed class EffectivePermissions(IReadOnlyList<EffectiveGrant> grants)
{
    /// <summary>
    /// The list of effective permission grants. Each entry has a permission name and an optional scope.
    /// A null scope means the permission is unrestricted (all resources allowed).
    /// </summary>
    public IReadOnlyList<EffectiveGrant> Grants { get; } = grants;
}

/// <summary>
/// A single effective permission grant: the permission name and the resource scope it applies to.
/// </summary>
public sealed record EffectiveGrant(string Permission, ResourceScope? Scope);
