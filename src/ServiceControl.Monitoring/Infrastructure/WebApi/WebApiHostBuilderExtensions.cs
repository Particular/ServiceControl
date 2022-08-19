namespace ServiceControl.Audit.Infrastructure.WebApi
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Web.Http.Controllers;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using ServiceControl.Monitoring.Infrastructure.WebApi;
    using ServiceBus.Management.Infrastructure.OWIN;

    static class WebApiHostBuilderExtensions
    {
        public static IHostBuilder UseWebApi(this IHostBuilder hostBuilder, string rootUrl, bool startOwinHost)
        {
            hostBuilder.ConfigureServices(services =>
            {
                RegisterInternalWebApiControllers(services);
                services.AddHostedService(sp =>
                {
                    var startup = new Startup(sp);
                    return new WebApiHostedService(rootUrl, startup);
                });
            });

            if (startOwinHost)
            {
                hostBuilder.ConfigureServices((ctx, serviceCollection) =>
                {
                    serviceCollection.AddHostedService(sp =>
                    {
                        var startup = new Startup(sp);
                        return new WebApiHostedService(rootUrl, startup);
                    });
                });
            }

            return hostBuilder;
        }

        static void RegisterInternalWebApiControllers(IServiceCollection serviceCollection)
        {
            var controllerTypes = Assembly.GetExecutingAssembly().DefinedTypes
                .Where(t => typeof(IHttpController).IsAssignableFrom(t) && t.Name.EndsWith("Controller", StringComparison.Ordinal));

            foreach (var controllerType in controllerTypes)
            {
                serviceCollection.AddScoped(controllerType);
            }
        }
    }
}