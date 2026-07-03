namespace ServiceControl.Monitoring.Infrastructure;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using ServiceControl.Hosting.ForwardedHeaders;
using ServiceControl.Hosting.Https;
using ServiceControl.Infrastructure;

public static class WebApplicationExtensions
{
    public static void UseServiceControlMonitoring(this WebApplication appBuilder, ForwardedHeadersSettings forwardedHeadersSettings, HttpsSettings httpsSettings, CorsSettings corsSettings)
    {
        // Surface the per-request id so callers can correlate and quote it. TraceIdentifier is stable
        // for the request; OnStarting sets it before the response flushes.
        appBuilder.Use((context, next) =>
        {
            context.Response.OnStarting(static state =>
            {
                var httpContext = (HttpContext)state;
                httpContext.Response.Headers["Request-Id"] = httpContext.TraceIdentifier;
                return Task.CompletedTask;
            }, context);

            return next(context);
        });

        appBuilder.UseServiceControlForwardedHeaders(forwardedHeadersSettings);
        appBuilder.UseServiceControlHttps(httpsSettings);

        appBuilder.UseHttpLogging();

        appBuilder.UseCors(policyBuilder =>
        {
            // AllowAnyOrigin is enabled by default.
            if (corsSettings.AllowAnyOrigin)
            {
                policyBuilder.AllowAnyOrigin();
            }
            else if (corsSettings.AllowedOrigins.Count > 0)
            {
                // Allow only specific origins (more secure, recommended for production)
                policyBuilder.WithOrigins([.. corsSettings.AllowedOrigins]);
                // Allow credentials (cookies, authorization headers) when specific origins are configured
                policyBuilder.AllowCredentials();
            }

            // Headers exposed to the client in the response (accessible via JavaScript)
            policyBuilder.WithExposedHeaders(["ETag", "Last-Modified", "Link", "Total-Count", "X-Particular-Version", "Request-Id"]);
            // Headers allowed in the request from the client
            policyBuilder.WithHeaders(["Origin", "X-Requested-With", "Content-Type", "Accept", "Authorization"]);
            // HTTP methods allowed for cross-origin requests
            policyBuilder.WithMethods(["POST", "GET", "PUT", "DELETE", "OPTIONS", "PATCH", "HEAD"]);
        });

        appBuilder.MapControllers();
    }
}