#nullable enable
namespace ServiceControl.Infrastructure.WebApi;

using System;

/// <summary>
/// Marks an API endpoint as requiring a specific permission.
/// Phase 1+ enforcement mechanisms (S2/S3/S4) read this attribute and enforce the named permission
/// via their respective authorization strategy.
/// <para>
/// The endpoint-completeness test treats the presence of this attribute as evidence that the
/// endpoint has been reviewed and has an authorization decision.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequirePermissionAttribute(string permission) : Attribute
{
    /// <summary>The permission required to access this endpoint (e.g. <c>messages:retry</c>).</summary>
    public string Permission { get; } = permission;
}
