namespace ServiceControl.Audit;

using Infrastructure.WebApi;
using Microsoft.AspNetCore.Builder;

public static class WebApplicationExtensions
{
    public static void UseServiceControlAudit(this WebApplication app)
    {
        app.UseResponseCompression();
        app.UseMiddleware<BodyUrlRouteFix>();
        app.UseHttpLogging();
        app.UseCors();
        app.MapControllers();
    }
}