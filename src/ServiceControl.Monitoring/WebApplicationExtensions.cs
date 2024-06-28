namespace ServiceControl.Monitoring.Infrastructure;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

public static class WebApplicationExtensions
{
    public static void UseServiceControlMonitoring(this WebApplication appBuilder)
    {
        appBuilder.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.All });

        appBuilder.UseHttpLogging();

        appBuilder.UseCors(policyBuilder =>
        {
            policyBuilder.AllowAnyOrigin();
            policyBuilder.WithExposedHeaders(["ETag", "Last-Modified", "Link", "Total-Count", "X-Particular-Version"]);
            policyBuilder.WithHeaders(["Origin", "X-Requested-With", "Content-Type", "Accept"]);
            policyBuilder.WithMethods(["POST", "GET", "PUT", "DELETE", "OPTIONS", "PATCH"]);
        });

        appBuilder.MapControllers();
    }
}