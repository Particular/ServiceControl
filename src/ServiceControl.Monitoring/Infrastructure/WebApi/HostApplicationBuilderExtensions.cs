namespace ServiceControl.Monitoring.Infrastructure.WebApi;

using System.Reflection;
using Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceControl.Monitoring.Infrastructure.Api;

public static class HostApplicationBuilderExtensions
{
    public static void AddServiceControlMonitoringApi(this IHostApplicationBuilder hostBuilder)
    {
        hostBuilder.Services.AddSingleton<IEndpointMetricsApi, EndpointMetricsApi>();

        var controllers = hostBuilder.Services.AddControllers(options =>
        {
            options.Filters.Add<XParticularVersionHttpHandler>();
            options.Filters.Add<CachingHttpHandler>();
        });
        controllers.AddApplicationPart(Assembly.GetExecutingAssembly());
        controllers.AddJsonOptions(options => options.JsonSerializerOptions.CustomizeDefaults());
    }
}