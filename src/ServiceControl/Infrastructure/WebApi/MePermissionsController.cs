#nullable enable
namespace ServiceControl.Infrastructure.WebApi;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using ServiceControl.Infrastructure.Auth.Rbac;

/// <summary>
/// Exposes the calling user's effective permission set.
/// Any authenticated user may query their own permissions — no specific permission is required.
/// ServicePulse consumes this endpoint to decide which UI controls to show.
/// </summary>
[ApiController]
[Route("api")]
[AuthenticatedOnly]
public class MePermissionsController(IPermissionEvaluator permissionEvaluator, Func<RbacPolicy> policyFactory) : ControllerBase
{
    /// <summary>
    /// Returns the effective permissions for the currently authenticated user.
    /// </summary>
    /// <response code="200">The user's effective permissions.</response>
    /// <response code="401">No valid bearer token was provided.</response>
    [HttpGet]
    [Route("me/permissions")]
    public ActionResult<PermissionsDescriptor> GetMyPermissions()
    {
        var policy = policyFactory();
        var effective = permissionEvaluator.Resolve(User);

        var permissions = effective.Grants
            .Select(g => new PermissionEntry(
                g.Permission,
                g.Scope != null
                    ? new ScopeDescriptor(g.Scope.Allow, g.Scope.Deny)
                    : null))
            .ToList();

        var descriptor = new PermissionsDescriptor(
            Version: policy.LoadedAt,
            User: User.FindFirst("sub")?.Value ?? User.Identity?.Name ?? "unknown",
            Permissions: permissions);

        return Ok(descriptor);
    }
}

/// <summary>The JSON shape returned by <c>GET /api/me/permissions</c>.</summary>
public sealed record PermissionsDescriptor(
    DateTimeOffset Version,
    string User,
    IReadOnlyList<PermissionEntry> Permissions);

/// <summary>A single effective permission grant in the descriptor response.</summary>
public sealed record PermissionEntry(string Permission, ScopeDescriptor? Scope);

/// <summary>The allow/deny scope attached to a permission entry (null = unrestricted).</summary>
public sealed record ScopeDescriptor(IReadOnlyList<string> Allow, IReadOnlyList<string> Deny);
