namespace ServiceControl;

using Infrastructure.SignalR;
using Infrastructure.WebApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

public static class WebApplicationExtensions
{
    public static void UseServiceControl(this WebApplication app)
    {
        app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.All });
        app.UseResponseCompression();
        app.UseMiddleware<BodyUrlRouteFix>();
        app.UseHttpLogging();
        app.MapHub<MessageStreamerHub>("/api/messagestream");
        app.UseCors();
    }
}