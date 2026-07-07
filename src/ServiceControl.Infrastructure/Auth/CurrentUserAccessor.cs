#nullable enable
namespace ServiceControl.Infrastructure.Auth;

using System.Security.Claims;

/// <summary>
/// Reads the subject id/name from the configured OIDC claim keys (the same keys
/// <c>PermissionVerbHandler</c> uses). Falls back to <see cref="AuditUser.Anonymous"/> rather than
/// throwing, so the action trail is still recorded when authentication is disabled.
/// </summary>
public sealed class CurrentUserAccessor(OpenIdConnectSettings oidcSettings) : ICurrentUserAccessor
{
    public AuditUser Resolve(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return AuditUser.Anonymous;
        }

        var id = principal.FindFirst(oidcSettings.SubjectIdClaim)?.Value;
        if (string.IsNullOrEmpty(id))
        {
            return AuditUser.Anonymous;
        }

        var name = principal.FindFirst(oidcSettings.SubjectNameClaim)?.Value;
        return new AuditUser(id, string.IsNullOrEmpty(name) ? id : name);
    }
}
