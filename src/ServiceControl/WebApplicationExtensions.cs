namespace ServiceControl;

using Infrastructure.SignalR;
using Infrastructure.WebApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

public static class WebApplicationExtensions
{
    public static void UseServiceControl(this WebApplication app, bool authenticationEnabled = false)
    {
        app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.All });
        app.UseResponseCompression();
        app.UseMiddleware<BodyUrlRouteFix>();
        app.UseHttpLogging();
        app.MapHub<MessageStreamerHub>("/api/messagestream");
        app.UseCors();

        // Always add middleware (harmless when not configured)
        app.UseAuthentication();
        app.UseAuthorization();

        // Only require authorization if authentication is enabled
        if (authenticationEnabled)
        {
            app.MapControllers().RequireAuthorization();
        }
        else
        {
            app.MapControllers();
        }
    }
}