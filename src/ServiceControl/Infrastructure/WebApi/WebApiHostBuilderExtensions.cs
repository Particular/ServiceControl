namespace ServiceControl.Infrastructure.WebApi
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Web.Http.Controllers;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using ServiceBus.Management.Infrastructure.OWIN;
    using ServiceControl.CompositeViews.Messages;

    static class WebApiHostBuilderExtensions
    {
        public static IHostBuilder UseWebApi(this IHostBuilder hostBuilder, List<Assembly> apiAssemblies, string rootUrl, bool startOwinHost)
        {
            foreach (var apiAssembly in apiAssemblies)
            {
                hostBuilder.ConfigureServices(serviceCollection =>
                {
                    RegisterAssemblyInternalWebApiControllers(serviceCollection, apiAssembly);
                    RegisterApiTypes(serviceCollection, apiAssembly);
                    RegisterConcreteTypes(serviceCollection, apiAssembly);
                });
            }

            if (startOwinHost)
            {
                hostBuilder.ConfigureServices((ctx, serviceCollection) =>
                {
                    serviceCollection.AddHostedService(sp =>
                    {
                        var startup = new Startup(sp, apiAssemblies);
                        return new WebApiHostedService(rootUrl, startup);
                    });
                });
            }

            return hostBuilder;
        }

        static void RegisterAssemblyInternalWebApiControllers(IServiceCollection serviceCollection, Assembly assembly)
        {
            var controllerTypes = assembly.DefinedTypes
                .Where(t => typeof(IHttpController).IsAssignableFrom(t) && t.Name.EndsWith("Controller", StringComparison.Ordinal));

            foreach (var controllerType in controllerTypes)
            {
                serviceCollection.AddScoped(controllerType);
            }
        }

        static void RegisterApiTypes(IServiceCollection serviceCollection, Assembly assembly)
        {
            var apiTypes = assembly.DefinedTypes
                .Where(ti => ti.IsClass &&
                            !ti.IsAbstract &&
                            !ti.IsGenericTypeDefinition &&
                            ti.GetInterfaces().Any(t => t == typeof(IApi)))
                .Select(ti => ti.AsType());


            foreach (var apiType in apiTypes)
            {
                if (!serviceCollection.Any(sd => sd.ServiceType == apiType))
                {
                    serviceCollection.AddSingleton(apiType);
                }
            }
        }

        static void RegisterConcreteTypes(IServiceCollection serviceCollection, Assembly assembly)
        {
            var concreteTypes = assembly.DefinedTypes
                .Where(ti => ti.IsClass &&
                            !ti.IsAbstract &&
                            !ti.IsSubclassOf(typeof(Delegate)) &&
                            !ti.IsGenericTypeDefinition &&
                            !ti.GetInterfaces().Any())
                .Select(ti => ti.AsType());


            foreach (var concreteType in concreteTypes)
            {
                if (!serviceCollection.Any(sd => sd.ServiceType == concreteType))
                {
                    serviceCollection.AddSingleton(concreteType);
                }
            }
        }
    }
}
