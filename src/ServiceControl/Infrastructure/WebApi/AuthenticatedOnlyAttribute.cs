namespace ServiceControl.Infrastructure.WebApi;

using System;

/// <summary>
/// Marks an API endpoint as reviewed: it requires authentication but no specific permission.
/// Applied to endpoints that serve user-specific data but must be accessible to any authenticated user
/// (e.g. <c>GET /api/me/permissions</c>).
/// <para>
/// The endpoint-completeness test (<see cref="Every_endpoint_declares_an_authorization_decision"/>)
/// accepts this attribute as an explicit opt-out from per-permission enforcement.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AuthenticatedOnlyAttribute : Attribute;
