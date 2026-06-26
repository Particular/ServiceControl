#nullable enable
namespace ServiceControl.Hosting.Auth;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using ServiceControl.Infrastructure.Auth;

/// <summary>
/// Projects the wired controller endpoints into the static <c>route ⇒ permission</c> table that backs
/// the <c>my/routes</c> manifest. Built once on first access (after endpoints are mapped) and cached
/// for the process lifetime — routes are compiled in and never change at runtime. Each endpoint
/// contributes one <see cref="RouteAuthInfo"/> per HTTP method, carrying the policy name from its
/// <c>[Authorize(Policy = …)]</c> attribute (the permission), whether it is <c>[AllowAnonymous]</c>,
/// and the normalized template. No-policy endpoints are authenticated-only, matching the
/// RequireAuthenticatedUser fallback policy.
/// </summary>
public sealed class RouteAuthorizationTable(EndpointDataSource endpointDataSource)
{
    readonly Lazy<IReadOnlyList<RouteAuthInfo>> entries = new(() => Build(endpointDataSource));

    public IReadOnlyList<RouteAuthInfo> Entries => entries.Value;

    static IReadOnlyList<RouteAuthInfo> Build(EndpointDataSource endpointDataSource)
    {
        var result = new List<RouteAuthInfo>();

        foreach (var endpoint in endpointDataSource.Endpoints.OfType<RouteEndpoint>())
        {
            // Only controller actions: skips the SignalR hub and other non-MVC endpoints.
            if (endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() is null)
            {
                continue;
            }

            var template = RouteTemplateNormalizer.Normalize(endpoint.RoutePattern.RawText ?? string.Empty);
            var allowAnonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            var requiredPermission = endpoint.Metadata
                .GetOrderedMetadata<IAuthorizeData>()
                .Select(authorize => authorize.Policy)
                .FirstOrDefault(policy => !string.IsNullOrEmpty(policy));

            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
                ?? [];

            foreach (var method in methods)
            {
                result.Add(new RouteAuthInfo(method, template, requiredPermission, allowAnonymous));
            }
        }

        return result;
    }
}
