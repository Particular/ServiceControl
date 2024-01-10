namespace ServiceControl;

using System.Threading.Tasks;
using Infrastructure.OWIN;
using Infrastructure.SignalR;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Persistence;

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
        // Initialized IDocumentStore, this is needed as many hosted services have (indirect) dependencies on it.
        await app.Services.GetRequiredService<IPersistenceLifecycle>().Initialize();
        await app.StartAsync();
    }
}