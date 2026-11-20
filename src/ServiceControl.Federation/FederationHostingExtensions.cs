namespace ServiceControl.Federation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Api;
using Configuration;
using Members;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Routing;

/// <summary>
/// Registration entry points used by the hosting ServiceControl primary instance, in the same way it
/// hosts the integrated ServicePulse. The host owns the cross-cutting middleware: forwarded headers,
/// CORS, authentication and authorization.
/// </summary>
public static class FederationHostingExtensions
{
    public static IServiceCollection AddServiceControlFederation(this IServiceCollection services, FederationSettings settings)
    {
        settings.Validate();

        services.AddSingleton(settings);
        services.AddSingleton<MemberRegistry>();
        services.AddSingleton<MemberGateway>();
        services.AddSingleton<OwnerCache>();
        services.AddSingleton<OwnerResolver>();
        services.AddSingleton<Federator>();
        services.AddHostedService<MemberHealthMonitor>();

        services
            .AddHttpClient(MemberGateway.HttpClientName, client => client.Timeout = System.Threading.Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });

        return services;
    }

    /// <summary>
    /// Maps both federated surfaces under their path bases. Routes a table does not list are answered
    /// by the lead member and logged, so a newer ServiceControl route keeps working rather than failing.
    /// </summary>
    public static IEndpointRouteBuilder MapServiceControlFederation(this IEndpointRouteBuilder endpoints)
    {
        var federator = endpoints.ServiceProvider.GetRequiredService<Federator>();
        var settings = federator.Settings;

        MapSurface(endpoints, federator, settings.PrimaryPathBase, PrimaryRoutes.All, Surface.Primary);
        MapSurface(endpoints, federator, settings.MonitoringPathBase, MonitoringRoutes.All, Surface.Monitoring);

        return endpoints;
    }

    static void MapSurface(IEndpointRouteBuilder endpoints, Federator federator, string prefix, IEnumerable<FederatedRoute> routes, Surface surface)
    {
        var group = endpoints.MapGroup(prefix);

        foreach (var route in routes)
        {
            var endpoint = group.MapMethods(route.Template, route.Methods, context => route.Handler(federator, context))
                .WithDisplayName($"Federation {surface} {string.Join('/', route.Methods)} {route.Template} [{route.Strategy}]");

            // The members decide what the caller may do; anonymous routes on the members stay anonymous here.
            if (route.Anonymous)
            {
                endpoint.AllowAnonymous();
            }
        }

        group.Map("{**unmapped}", context =>
        {
            federator.Logger.LogWarning("No federation strategy for {Method} {Path}; answering from the lead member", context.Request.Method, context.Request.Path);
            return federator.Lead(context, surface);
        });
    }

    public static IReadOnlyList<FederatedRoute> AllRoutes => [.. PrimaryRoutes.All.Concat(MonitoringRoutes.All)];
}
