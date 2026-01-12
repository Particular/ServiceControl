namespace ServiceControl.Audit;

using Infrastructure.WebApi;
using Microsoft.AspNetCore.Builder;
using ServiceControl.Hosting.ForwardedHeaders;
using ServiceControl.Hosting.Https;
using ServiceControl.Infrastructure;

public static class WebApplicationExtensions
{
    public static void UseServiceControlAudit(this WebApplication app, ForwardedHeadersSettings forwardedHeadersSettings, HttpsSettings httpsSettings)
    {
        app.UseServiceControlForwardedHeaders(forwardedHeadersSettings);
        app.UseServiceControlHttps(httpsSettings);
        app.UseResponseCompression();
        app.UseMiddleware<BodyUrlRouteFix>();
        app.UseHttpLogging();
        app.UseCors();
        app.MapControllers();
    }
}