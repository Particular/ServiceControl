namespace ServiceControl.Infrastructure.WebApi
{
    using System.Net.Mime;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;

    class AppConstantsMiddleware
    {
        readonly RequestDelegate next;
        readonly string content;
        static AppConstantsMiddleware() => FileVersion = ServiceControlVersion.GetFileVersion();
        static readonly string FileVersion;

        public AppConstantsMiddleware(RequestDelegate next)
        {
            this.next = next;

            var settings = ServicePulseSettings.GetFromEnvironmentVariables();
            var constants = new
            {
                default_route = settings.DefaultRoute,
                service_control_url = "api/",
                monitoring_urls = new[] { settings.MonitoringUri.ToString() },
                showPendingRetry = settings.ShowPendingRetry,
                version = FileVersion,
                embedded = true
            };
            var options = new JsonSerializerOptions { PropertyNamingPolicy = null };

            content = $"window.defaultConfig = {JsonSerializer.Serialize(constants, options)}";
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments("/js/app.constants.js"))
            {
                context.Response.ContentType = MediaTypeNames.Text.JavaScript;

                await context.Response.WriteAsync(content);
                return;
            }

            await next(context);
        }
    }
}
