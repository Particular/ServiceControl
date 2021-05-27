namespace ServiceControl.Audit.Infrastructure.WebApi
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Web.Http.Controllers;
    using Auditing.MessagesView;
    using Autofac;
    using Autofac.Core.Activators.Reflection;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using OWIN;

    static class WebApiHostBuilderExtensions
    {
        public static IHostBuilder UseWebApi(this IHostBuilder hostBuilder,
            List<Action<ContainerBuilder>> registrations, string rootUrl, bool startOwinHost)
        {
            registrations.Add(RegisterInternalWebApiControllers);
            registrations.Add(cb => cb.RegisterModule<ApisModule>());

            Startup startup = null;

            registrations.Add(cb =>
            {
                cb.RegisterBuildCallback(c => { startup = new Startup(c); });
            });

            if (startOwinHost)
            {
                hostBuilder.ConfigureServices((ctx, serviceCollection) =>
                {
                    serviceCollection.AddHostedService(sp => new WebApiHostedService(rootUrl, startup));
                });
            }

            return hostBuilder;
        }

        static void RegisterInternalWebApiControllers(ContainerBuilder containerBuilder)
        {
            var controllerTypes = Assembly.GetExecutingAssembly().DefinedTypes
                .Where(t => typeof(IHttpController).IsAssignableFrom(t) && t.Name.EndsWith("Controller", StringComparison.Ordinal));

            foreach (var controllerType in controllerTypes)
            {
                containerBuilder.RegisterType(controllerType).FindConstructorsWith(new AllConstructorFinder());
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