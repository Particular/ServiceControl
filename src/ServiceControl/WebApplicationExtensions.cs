namespace ServiceControl;

using System.Threading.Tasks;
using Infrastructure.SignalR;
using Infrastructure.WebApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using ServiceControl.Hosting.ForwardedHeaders;
using ServiceControl.Hosting.Https;
using ServiceControl.Infrastructure;

public static class WebApplicationExtensions
{
    public static void UseServiceControl(this WebApplication app, ForwardedHeadersSettings forwardedHeadersSettings, HttpsSettings httpsSettings)
    {
        // Surface the per-request id (same value used as the audit operation id) so callers can correlate
        // and quote it. TraceIdentifier is stable for the request; OnStarting sets it before the response flushes.
        app.Use((context, next) =>
        {
            context.Response.OnStarting(static state =>
            {
                var httpContext = (HttpContext)state;
                httpContext.Response.Headers["Request-Id"] = httpContext.TraceIdentifier;
                return Task.CompletedTask;
            }, context);

            return next(context);
        });

        app.UseServiceControlForwardedHeaders(forwardedHeadersSettings);
        app.UseServiceControlHttps(httpsSettings);
        app.UseResponseCompression();
        app.UseMiddleware<BodyUrlRouteFix>();
        app.UseHttpLogging();
        app.MapHub<MessageStreamerHub>("/api/messagestream");
        app.UseCors();
        app.MapControllers();
    }
}