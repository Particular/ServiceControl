namespace ServiceControl;

using Infrastructure.OWIN;
using Infrastructure.SignalR;
using Microsoft.AspNetCore.Builder;

public static class WebApplicationExtensions
{
    public static void UseServiceControl(this WebApplication app)
    {
        app.UseResponseCompression();
        app.UseMiddleware<BodyUrlRouteFix>();
        app.UseHttpLogging();
        app.MapHub<MessageStreamerHub>("/api/messagestream");
        app.UseCors();
        app.MapControllers();
    }
}