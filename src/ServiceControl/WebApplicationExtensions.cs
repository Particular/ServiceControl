namespace ServiceControl;

using Infrastructure.SignalR;
using Infrastructure.WebApi;
using Microsoft.AspNetCore.Builder;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Hosting.ForwardedHeaders;
using ServiceControl.Hosting.Https;
using ServiceControl.Infrastructure;

public static class WebApplicationExtensions
{
    public static void UseServiceControl(this WebApplication app, ForwardedHeadersSettings forwardedHeadersSettings, HttpsSettings httpsSettings, Settings settings)
    {
        app.UseServiceControlForwardedHeaders(forwardedHeadersSettings);
        app.UseServiceControlHttps(httpsSettings);
        app.UseResponseCompression();
        app.UseMiddleware<BodyUrlRouteFix>();
        app.UseHttpLogging();
        app.MapHub<MessageStreamerHub>("/api/messagestream");
        app.UseCors();
        app.MapControllers();

        if (settings.EnableMcpServer)
        {
            app.MapMcp();
        }
    }
}