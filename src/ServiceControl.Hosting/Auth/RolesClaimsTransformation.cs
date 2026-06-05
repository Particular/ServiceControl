#nullable enable
namespace ServiceControl.Hosting.Auth;

using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using ServiceControl.Infrastructure.Auth;

/// <summary>
/// Normalises per-IdP role claim shapes into a flat set of <c>roles</c> claims that
/// <see cref="PermissionVerbHandler"/> can read directly. The source path is configured via
/// <c>Authentication.RolesClaim</c> (default <c>realm_access.roles</c> — the Keycloak out-of-box
/// shape). Flat claim names work too (<c>roles</c> for Keycloak with a "User Realm Role" mapper or
/// Microsoft Entra ID app roles, <c>cognito:groups</c> for AWS Cognito).
/// <para>
/// ASP.NET may invoke <see cref="TransformAsync"/> multiple times for the same principal; a sentinel
/// claim makes the transformation idempotent and returns the same principal on subsequent calls.
/// </para>
/// </summary>
public sealed class RolesClaimsTransformation(string rolesClaimPath) : IClaimsTransformation
{
    const string SentinelClaimType = "_roles_transformed";
    // The sentinel's value is irrelevant; only the claim's presence matters. A non-empty
    // placeholder is required because a Claim value cannot be null.
    const string SentinelClaimValue = "1";
    const string RoleClaimType = "roles";

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var isAuthenticated = principal.Identity?.IsAuthenticated == true;
        if (!isAuthenticated || AlreadyTransformed(principal))
        {
            return Task.FromResult(principal);
        }

        var roles = RolesClaimExtractor.Extract(principal, rolesClaimPath);

        var claims = new Claim[roles.Count + 1];
        claims[0] = new Claim(SentinelClaimType, SentinelClaimValue);
        for (var i = 0; i < roles.Count; i++)
        {
            claims[i + 1] = new Claim(RoleClaimType, roles[i]);
        }

        // Build a new principal so the original (cached) instance is left untouched.
        var transformed = new ClaimsPrincipal(principal.Identities.ToArray());
        transformed.AddIdentity(new ClaimsIdentity(claims));
        return Task.FromResult(transformed);
    }

    // True once this transformation has stamped its sentinel claim, keeping TransformAsync
    // idempotent across the repeated calls ASP.NET makes for the same principal.
    static bool AlreadyTransformed(ClaimsPrincipal principal) =>
        principal.HasClaim(SentinelClaimType, SentinelClaimValue);
}
