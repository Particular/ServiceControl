namespace ServiceControl.Audit;

using Microsoft.AspNetCore.Builder;
using Infrastructure.OWIN;

public static class WebApplicationExtensions
{
    public static void UseServiceControlAudit(this WebApplication app)
    {
        app.UseResponseCompression();
        app.UseMiddleware<BodyUrlRouteFix>();
        app.UseMiddleware<LogApiCalls>();
        app.UseCors();
        app.MapControllers();
    }
}