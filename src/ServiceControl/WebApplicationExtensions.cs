namespace ServiceControl;

using System.Threading.Tasks;
using Infrastructure.OWIN;
using Infrastructure.SignalR;
using Microsoft.AspNetCore.Builder;

public static class WebApplicationExtensions
{
    public static void UseServiceControl(this WebApplication app)
    {
        app.UseResponseCompression();
        app.UseMiddleware<BodyUrlRouteFix>();
        app.UseMiddleware<LogApiCalls>();
        app.MapHub<MessageStreamerHub>("/api/messagestream");
        app.UseCors();
        app.MapControllers();
    }

    public static async Task StartServiceControl(this WebApplication app)
    {
        await app.StartAsync();
    }
}