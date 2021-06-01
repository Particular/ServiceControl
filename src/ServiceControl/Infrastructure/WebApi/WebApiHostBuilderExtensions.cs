namespace ServiceControl.Infrastructure.WebApi
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Web.Http.Controllers;
    using Autofac;
    using Autofac.Core.Activators.Reflection;
    using Autofac.Features.ResolveAnything;
    using MessageFailures.Api;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using ServiceBus.Management.Infrastructure.OWIN;

    static class WebApiHostBuilderExtensions
    {
        public static IHostBuilder UseWebApi(this IHostBuilder hostBuilder, List<Assembly> apiAssemblies, string rootUrl, bool startOwinHost)
        {
            foreach (var apiAssembly in apiAssemblies)
            {
                hostBuilder.ConfigureContainer<ContainerBuilder>(cb =>
                {
                    RegisterAssemblyInternalWebApiControllers(cb, apiAssembly);
                    cb.RegisterModule(new ApisModule(apiAssembly));
                    cb.RegisterSource(new AnyConcreteTypeNotAlreadyRegisteredSource(type =>
                        type.Assembly == apiAssembly && type.GetInterfaces().Any() == false));
                });
            }

            if (startOwinHost)
            {
                hostBuilder.ConfigureServices((ctx, serviceCollection) =>
                {
                    serviceCollection.AddHostedService(sp =>
                    {
                        var startup = new Startup(sp.GetRequiredService<ILifetimeScope>(), apiAssemblies);
                        return new WebApiHostedService(rootUrl, startup);
                    });
                });
            }

            return hostBuilder;
        }

        static void RegisterAssemblyInternalWebApiControllers(ContainerBuilder containerBuilder, Assembly assembly)
        {
            var controllerTypes = assembly.DefinedTypes
                .Where(t => typeof(IHttpController).IsAssignableFrom(t) && t.Name.EndsWith("Controller", StringComparison.Ordinal));

            foreach (var controllerType in controllerTypes)
            {
                containerBuilder.RegisterType(controllerType).FindConstructorsWith(new AllConstructorFinder()).ExternallyOwned();
            }
        }

        class AllConstructorFinder : IConstructorFinder
        {
            public ConstructorInfo[] FindConstructors(Type targetType)
            {
                var result = Cache.GetOrAdd(targetType, t => t.GetTypeInfo().DeclaredConstructors.ToArray());

                return result.Length > 0 ? result : throw new Exception($"No constructor found for type {targetType.FullName}");
            }

            static readonly ConcurrentDictionary<Type, ConstructorInfo[]> Cache = new ConcurrentDictionary<Type, ConstructorInfo[]>();
        }
    }
}
