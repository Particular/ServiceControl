#nullable enable
namespace ServiceControl.Infrastructure.Auth;

using System.Security.Claims;

/// <summary>Resolves the audited <see cref="AuditUser"/> from the current request principal.</summary>
public interface ICurrentUserAccessor
{
    /// <summary>Returns the principal's subject id/name, or <see cref="AuditUser.Anonymous"/> when there is no identified principal.</summary>
    AuditUser Resolve(ClaimsPrincipal? principal);
}
