namespace ServiceControl.Monitoring.Infrastructure.WebApi;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Infrastructure;

public static class HostApplicationBuilderExtensions
{
    public static void AddServiceControlMonitoringApi(this IHostApplicationBuilder hostBuilder)
    {
        var controllers = hostBuilder.Services.AddControllers(options =>
        {
            options.Filters.Add<XParticularVersionHttpHandler>();
            options.Filters.Add<CachingHttpHandler>();
        });
        controllers.AddApplicationPart(typeof(Settings).Assembly);
        controllers.AddJsonOptions(options => options.JsonSerializerOptions.CustomizeDefaults());
    }
}