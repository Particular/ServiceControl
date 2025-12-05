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
            if (corsSettings.AllowAnyOrigin)
            {
                policyBuilder.AllowAnyOrigin();
            }
            else if (corsSettings.AllowedOrigins.Count > 0)
            {
                policyBuilder.WithOrigins([.. corsSettings.AllowedOrigins]);
                policyBuilder.AllowCredentials();
            }

            policyBuilder.WithExposedHeaders(["ETag", "Last-Modified", "Link", "Total-Count", "X-Particular-Version"]);
            policyBuilder.WithHeaders(["Origin", "X-Requested-With", "Content-Type", "Accept", "Authorization"]);
            policyBuilder.WithMethods(["POST", "GET", "PUT", "DELETE", "OPTIONS", "PATCH"]);
        });

        appBuilder.MapControllers();
    }
}