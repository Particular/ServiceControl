#nullable enable
namespace ServiceControl.Hosting.Auth;

using Microsoft.AspNetCore.Authorization;

/// <summary>
/// An <see cref="IAuthorizationRequirement"/> that carries the permission string enforced by a
/// <c>[Authorize(Policy = "&lt;permission&gt;")]</c> attribute (e.g. <c>error:messages:view</c>).
/// Evaluated by <see cref="PermissionVerbHandler"/>.
/// </summary>
public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
