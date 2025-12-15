namespace ServiceControl.Hosting.ForwardedHeaders;

using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Hosting;
using ServiceControl.Infrastructure;

public static class WebApplicationExtensions
{
    public static void UseServiceControlForwardedHeaders(this WebApplication app, ForwardedHeadersSettings settings)
    {
        // Register debug endpoint first (before early return) so it's always available in Development
        if (app.Environment.IsDevelopment())
        {
            app.MapGet("/debug/request-info", (HttpContext context) =>
            {
                var remoteIp = context.Connection.RemoteIpAddress;

                // Processed values (after ForwardedHeaders middleware, if enabled)
                var scheme = context.Request.Scheme;
                var host = context.Request.Host.ToString();
                var remoteIpAddress = remoteIp?.ToString();

                // Raw forwarded headers (what remains after middleware processing)
                // Note: When ForwardedHeaders middleware processes headers from a trusted proxy,
                // it consumes (removes) them from the request headers
                var xForwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
                var xForwardedProto = context.Request.Headers["X-Forwarded-Proto"].ToString();
                var xForwardedHost = context.Request.Headers["X-Forwarded-Host"].ToString();

                // Configuration
                var knownProxies = settings.KnownProxies.Select(p => p.ToString()).ToArray();
                var knownNetworks = settings.KnownNetworks.ToArray();

                return new
                {
                    processed = new { scheme, host, remoteIpAddress },
                    rawHeaders = new { xForwardedFor, xForwardedProto, xForwardedHost },
                    configuration = new
                    {
                        enabled = settings.Enabled,
                        trustAllProxies = settings.TrustAllProxies,
                        knownProxies,
                        knownNetworks
                    }
                };
            });
        }

        if (!settings.Enabled)
        {
            return;
        }

        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.All
        };

        // Clear default loopback-only restrictions
        options.KnownProxies.Clear();
        options.KnownNetworks.Clear();

        if (settings.TrustAllProxies)
        {
            // Trust all proxies: remove hop limit
            options.ForwardLimit = null;
        }
        else
        {
            // Only trust explicitly configured proxies and networks
            foreach (var proxy in settings.KnownProxies)
            {
                options.KnownProxies.Add(proxy);
            }

            foreach (var network in settings.KnownNetworks)
            {
                options.KnownNetworks.Add(IPNetwork.Parse(network));
            }
        }

        app.UseForwardedHeaders(options);
    }
}
