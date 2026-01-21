namespace ServiceControl.Monitoring.Infrastructure;

using Microsoft.AspNetCore.Builder;
using ServiceControl.Hosting.ForwardedHeaders;
using ServiceControl.Hosting.Https;
using ServiceControl.Infrastructure;

public static class WebApplicationExtensions
{
    public static void UseServiceControlMonitoring(this WebApplication appBuilder, ForwardedHeadersSettings forwardedHeadersSettings, HttpsSettings httpsSettings, CorsSettings corsSettings)
    {
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
            policyBuilder.WithExposedHeaders(["ETag", "Last-Modified", "Link", "Total-Count", "X-Particular-Version"]);
            // Headers allowed in the request from the client
            policyBuilder.WithHeaders(["Origin", "X-Requested-With", "Content-Type", "Accept", "Authorization"]);
            // HTTP methods allowed for cross-origin requests
            policyBuilder.WithMethods(["POST", "GET", "PUT", "DELETE", "OPTIONS", "PATCH", "HEAD"]);
        });

        appBuilder.MapControllers();
    }
}