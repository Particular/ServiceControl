#nullable enable
namespace ServiceControl.Infrastructure.Auth;

using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

/// <summary>
/// Normalizes an ASP.NET route pattern's raw text into the template form ServicePulse matches its
/// outgoing requests against: inline constraints/defaults/optional markers and catch-all stars are
/// removed (parameter names kept), and a single leading slash is guaranteed. For example
/// <c>api/errors/{id:required:minlength(1)}/retry</c> → <c>/api/errors/{id}/retry</c>.
/// </summary>
public static partial class RouteTemplateNormalizer
{
    public static string Normalize(string rawTemplate)
    {
        var stripped = ParameterToken().Replace(rawTemplate, "{${name}}");
        return stripped.StartsWith('/') ? stripped : "/" + stripped;
    }

    // Matches a single route parameter token: optional catch-all star(s), the parameter name, then
    // anything up to the closing brace (constraints, default value, optional marker).
    [GeneratedRegex(@"\{\*{0,2}(?<name>[A-Za-z0-9_]+)[^}]*\}")]
    private static partial Regex ParameterToken();
}

/// <summary>A route the server hosts, with the authorization metadata read from its endpoint.</summary>
public sealed record RouteAuthInfo(string Method, string UrlTemplate, string? RequiredPermission, bool AllowAnonymous);

/// <summary>
/// A single allowed-route entry returned to the client. The JSON field names are pinned with
/// <see cref="JsonPropertyName"/> so the manifest has one stable shape regardless of each host's
/// global JSON naming policy (the Primary instance serializes snake_case, the Monitoring instance
/// camelCase). Without this the same contract would emit <c>url_template</c> on one instance and
/// <c>urlTemplate</c> on another, and clients that merge both would silently drop half the routes.
/// </summary>
public sealed record RouteManifestEntry(
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("url_template")] string UrlTemplate);

/// <summary>
/// Projects the route table down to the entries a caller may invoke. A route is included when it is
/// anonymous, requires only authentication (no specific permission), or its required permission is in
/// the caller's effective set. Enforcement and this projection read the same inputs, so the advertised
/// manifest cannot drift from what the server actually allows.
/// </summary>
public static class RouteManifestFilter
{
    public static IReadOnlyList<RouteManifestEntry> Filter(
        IEnumerable<RouteAuthInfo> routes,
        IReadOnlySet<string> effectivePermissions) =>
        routes
            .Where(route => route.AllowAnonymous
                || route.RequiredPermission is null
                || effectivePermissions.Contains(route.RequiredPermission))
            .Select(route => new RouteManifestEntry(route.Method, route.UrlTemplate))
            .ToList();
}
