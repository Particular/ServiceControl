namespace ServiceControl.Infrastructure.WebApi
{
    using System.Net.Mime;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;

    class AppConstantsMiddleware
    {
        readonly RequestDelegate next;

        static AppConstantsMiddleware() => FileVersion = ServiceControlVersion.GetFileVersion();
        static readonly string FileVersion;
        public AppConstantsMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments("/js/app.constants.json"))
            {
                // TODO: Populate some of these settings dynamically from the config settings the user has set
                var constants = new
                {
                    default_route = "/dashboard",
                    service_control_url = "api/",
                    monitoring_url = "http://localhost:33633/",
                    showPendingRetry = true,
                    version = FileVersion
                };

                context.Response.ContentType = MediaTypeNames.Text.JavaScript;
                var options = new JsonSerializerOptions { PropertyNamingPolicy = null };
                await context.Response.WriteAsync(JsonSerializer.Serialize(constants, options));
                return;
            }

            await next(context);
        }
    }
}
