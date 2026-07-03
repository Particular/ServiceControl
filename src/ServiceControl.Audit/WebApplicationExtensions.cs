namespace ServiceControl.Audit;

using System.Threading.Tasks;
using Infrastructure.WebApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using ServiceControl.Hosting.ForwardedHeaders;
using ServiceControl.Hosting.Https;
using ServiceControl.Infrastructure;

public static class WebApplicationExtensions
{
    public static void UseServiceControlAudit(this WebApplication app, ForwardedHeadersSettings forwardedHeadersSettings, HttpsSettings httpsSettings)
    {
        // Surface the per-request id so callers can correlate and quote it. TraceIdentifier is stable
        // for the request; OnStarting sets it before the response flushes.
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
        app.UseCors();
        app.MapControllers();
    }
}