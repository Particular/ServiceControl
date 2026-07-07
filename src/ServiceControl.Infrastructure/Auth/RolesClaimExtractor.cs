#nullable enable
namespace ServiceControl.Infrastructure.Auth;

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;

/// <summary>
/// Reads role values out of a <see cref="ClaimsPrincipal"/> at a configurable path.
/// Supports a flat claim name (<c>roles</c>) or a dotted path into a nested JSON object claim
/// (<c>realm_access.roles</c>). Used by <c>RolesClaimsTransformation</c> to normalize per-IdP token
/// shapes (Keycloak, Microsoft Entra ID, AWS Cognito, etc.) into a canonical set of role values.
/// </summary>
public static class RolesClaimExtractor
{
    /// <summary>
    /// Extracts every role value reachable at <paramref name="rolesClaimPath"/> on <paramref name="principal"/>.
    /// Returns an empty list when the path is absent or the value cannot be interpreted as a string or
    /// array of strings — never throws on malformed input.
    /// </summary>
    public static IReadOnlyList<string> Extract(ClaimsPrincipal principal, string rolesClaimPath)
    {
        if (principal is null || string.IsNullOrWhiteSpace(rolesClaimPath))
        {
            return Array.Empty<string>();
        }

        var segments = rolesClaimPath.Split('.');
        var topClaimType = segments[0];
        var results = new List<string>();

        foreach (var claim in principal.FindAll(topClaimType))
        {
            if (segments.Length == 1)
            {
                AddFlatClaimValues(results, claim.Value);
            }
            else
            {
                AddNestedClaimValues(results, claim.Value, segments);
            }
        }

        return results;
    }

    static void AddFlatClaimValues(List<string> results, string claimValue)
    {
        // Flat claim values are typically a single role string per claim (the JWT bearer middleware
        // explodes a top-level JSON array of strings into one claim per element). The fallback path
        // handles the rare case where an IdP serialises the array into a single claim value.
        if (LooksLikeJsonArray(claimValue))
        {
            if (TryParse(claimValue, out var doc))
            {
                using (doc)
                {
                    AppendStringArray(results, doc.RootElement);
                }
                return;
            }
        }

        results.Add(claimValue);
    }

    static void AddNestedClaimValues(List<string> results, string claimValue, string[] segments)
    {
        if (!TryParse(claimValue, out var doc))
        {
            return;
        }

        using (doc)
        {
            var node = doc.RootElement;
            for (var i = 1; i < segments.Length; i++)
            {
                if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(segments[i], out var next))
                {
                    return;
                }
                node = next;
            }

            AppendStringOrArray(results, node);
        }
    }

    static void AppendStringOrArray(List<string> results, JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.String)
        {
            var single = node.GetString();
            if (!string.IsNullOrEmpty(single))
            {
                results.Add(single);
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            AppendStringArray(results, node);
        }
    }

    static void AppendStringArray(List<string> results, JsonElement array)
    {
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString();
                if (!string.IsNullOrEmpty(value))
                {
                    results.Add(value);
                }
            }
        }
    }

    static bool LooksLikeJsonArray(string value)
    {
        var trimmed = value.AsSpan().TrimStart();
        return trimmed.Length > 0 && trimmed[0] == '[';
    }

    static bool TryParse(string value, out JsonDocument document)
    {
        try
        {
            document = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            document = null!;
            return false;
        }
    }
}
