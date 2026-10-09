namespace ServiceControl.AcceptanceTesting.OpenIdConnect;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

/// <summary>
/// Finds every route of a running instance that is not marked <c>[AllowAnonymous]</c>, and sends each
/// one a request without a token. This covers every endpoint, so a new or changed authorization
/// policy cannot let anonymous callers in without a test failing.
/// </summary>
public static class ProtectedRoutes
{
    /// <summary>
    /// Sends a request without a token to every protected route, and returns each route that did not
    /// answer 401, as "METHOD /path → status".
    /// </summary>
    public static async Task<IReadOnlyList<string>> FindRoutesNotRejectingAnonymousRequests(
        HttpClient client,
        EndpointDataSource endpointDataSource,
        CancellationToken cancellationToken = default)
    {
        var routes = endpointDataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [HttpMethods.Get])
                .Select(method => (Method: method, Path: BuildPath(endpoint.RoutePattern))))
            .Distinct()
            .ToList();

        if (routes.Count == 0)
        {
            throw new InvalidOperationException("No protected routes found. The endpoint data source is empty or every route allows anonymous access.");
        }

        var notRejected = new List<string>();

        foreach (var (method, path) in routes)
        {
            using var response = await OpenIdConnectAssertions.SendRequestWithoutAuth(client, new HttpMethod(method), path, cancellationToken);

            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                notRejected.Add($"{method} {path} → {(int)response.StatusCode}");
            }
        }

        return notRejected;
    }

    // Fills each route parameter with a value that satisfies its constraint, so the request matches the
    // route and reaches the authorization middleware instead of failing with 404.
    static string BuildPath(RoutePattern pattern)
    {
        var segments = pattern.PathSegments.Select(segment => string.Concat(segment.Parts.Select(part => part switch
        {
            RoutePatternLiteralPart literal => literal.Content,
            RoutePatternSeparatorPart separator => separator.Content,
            RoutePatternParameterPart parameter => SampleValue(parameter),
            _ => throw new NotSupportedException($"Unsupported route pattern part: {part.GetType().Name}")
        })));

        return "/" + string.Join('/', segments);
    }

    static string SampleValue(RoutePatternParameterPart parameter)
    {
        var constraints = parameter.ParameterPolicies.Select(policy => policy.Content ?? string.Empty).ToArray();

        if (constraints.Contains("guid", StringComparer.OrdinalIgnoreCase))
        {
            return Guid.Empty.ToString();
        }

        if (constraints.Any(constraint => constraint is "int" or "long"))
        {
            return "1";
        }

        if (constraints.Contains("bool", StringComparer.OrdinalIgnoreCase))
        {
            return bool.TrueString;
        }

        return "x";
    }
}
