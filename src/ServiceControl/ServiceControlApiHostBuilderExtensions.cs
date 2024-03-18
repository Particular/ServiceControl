namespace Particular.ServiceControl
{
    using global::ServiceControl.Api;
    using global::ServiceControl.Infrastructure.Api;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    static class ServiceControlApiHostBuilderExtensions
    {
        public static void AddServiceControlApis(this IHostApplicationBuilder hostBuilder)
        {
            hostBuilder.Services.AddSingleton<IConfigurationApi, ConfigurationApi>();
            hostBuilder.Services.AddSingleton<IAuditCountApi, AuditCountApi>();
            hostBuilder.Services.AddSingleton<IEndpointsApi, EndpointsApi>();
        }
    }
}