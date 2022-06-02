namespace ServiceControl.Audit.Infrastructure.WebApi
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Web.Http.Controllers;
    using Auditing.MessagesView;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using OWIN;

    static class WebApiHostBuilderExtensions
    {
        public static IHostBuilder UseWebApi(this IHostBuilder hostBuilder, string rootUrl, bool startOwinHost)
        {
            hostBuilder.ConfigureServices(services =>
            {
                RegisterInternalWebApiControllers(services);
                var apiTypes = Assembly.GetExecutingAssembly().GetTypes().Where(t => typeof(IApi).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract);
                foreach (var apiType in apiTypes)
                {
                    services.AddTransient(apiType);
                    foreach (var i in apiType.GetInterfaces())
                    {
                        services.AddTransient(i, sp => sp.GetRequiredService(apiType));
                    }
                }
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