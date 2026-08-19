namespace ServiceControl;

using Infrastructure.WebApi;
using Microsoft.AspNetCore.Builder;
using ServiceControl.Hosting.ForwardedHeaders;
using ServiceControl.Hosting.Https;
using ServiceControl.Hosting.RequestId;
using ServiceControl.Infrastructure;
using ServiceControl.Infrastructure.Health;

public static class WebApplicationExtensions
{
    public static void UseServiceControl(this WebApplication app, ForwardedHeadersSettings forwardedHeadersSettings, HttpsSettings httpsSettings)
    {
        app.UseRequestIdHeader();
        app.UseServiceControlForwardedHeaders(forwardedHeadersSettings);
        app.UseServiceControlHttps(httpsSettings);
        app.UseResponseCompression();
        app.UseMiddleware<BodyUrlRouteFix>();
        app.UseHttpLogging();
        app.UseCors();
        app.MapControllers();
        app.MapServiceControlHealthChecks();
    }
}