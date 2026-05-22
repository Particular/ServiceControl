#nullable enable
namespace ServiceControl.Hosting.Auth;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;

/// <summary>
/// An <see cref="IClaimsTransformation"/> that flattens Keycloak's nested
/// <c>realm_access.roles</c> JSON claim into individual <c>role</c> claims,
/// making them available to policy matching and <see cref="ServiceControl.Infrastructure.Auth.Rbac.IPermissionEvaluator"/>.
/// <para>
/// Keycloak sets <c>MapInboundClaims = false</c>, so the <c>realm_access</c>
/// claim arrives as a raw JSON string. This transformation unpacks it.
/// The transformation is idempotent — roles already present as <c>role</c> claims
/// are not added again.
/// </para>
/// </summary>
public class RealmAccessClaimsTransformation : IClaimsTransformation
{
    const string RealmAccessClaimType = "realm_access";
    const string RoleClaimType = "role";

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var realmAccessClaim = principal.FindFirst(RealmAccessClaimType);
        if (realmAccessClaim == null)
        {
            return Task.FromResult(principal);
        }

        var roles = ParseRoles(realmAccessClaim.Value);
        if (roles == null || roles.Count == 0)
        {
            return Task.FromResult(principal);
        }

        // Collect existing role claims to avoid duplicates (idempotent)
        var existingRoles = new HashSet<string>(principal.FindAll(RoleClaimType)
            .Select(c => c.Value), StringComparer.Ordinal);

        var claimsToAdd = roles
            .Where(r => !existingRoles.Contains(r))
            .Select(r => new Claim(RoleClaimType, r))
            .ToList();

        if (claimsToAdd.Count == 0)
        {
            return Task.FromResult(principal);
        }

        // Clone the identity and add the new role claims
        var identity = new ClaimsIdentity(principal.Identity);
        identity.AddClaims(claimsToAdd);

        return Task.FromResult(new ClaimsPrincipal(identity));
    }

    static List<string>? ParseRoles(string realmAccessJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(realmAccessJson);
            if (doc.RootElement.TryGetProperty("roles", out var rolesElement) &&
                rolesElement.ValueKind == JsonValueKind.Array)
            {
                var roles = new List<string>();
                foreach (var role in rolesElement.EnumerateArray())
                {
                    var value = role.GetString();
                    if (value != null)
                    {
                        roles.Add(value);
                    }
                }
                return roles;
            }
        }
        catch (JsonException)
        {
            // Malformed realm_access claim — skip transformation
        }
        return null;
    }
}
