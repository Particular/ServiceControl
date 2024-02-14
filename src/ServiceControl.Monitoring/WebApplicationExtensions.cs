namespace ServiceControl.Monitoring.Infrastructure;

using Microsoft.AspNetCore.Builder;

public static class WebApplicationExtensions
{
    public static void UseServiceControlMonitoring(this WebApplication appBuilder)
    {
        appBuilder.UseHttpLogging();

        appBuilder.UseCors(policyBuilder =>
        {
            // TODO verify that the default is no headers and no methods allowed
            //builder.AllowAnyHeader();
            //builder.AllowAnyMethod();
            policyBuilder.AllowAnyOrigin();
            policyBuilder.WithExposedHeaders(["ETag", "Last-Modified", "Link", "Total-Count", "X-Particular-Version"]);
            policyBuilder.WithHeaders(["Origin", "X-Requested-With", "Content-Type", "Accept"]);
            policyBuilder.WithMethods(["POST", "GET", "PUT", "DELETE", "OPTIONS", "PATCH"]);
        });

        appBuilder.MapControllers();
    }
}