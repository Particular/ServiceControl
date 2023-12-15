namespace ServiceControl.Infrastructure.WebApi
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using ServiceControl.CompositeViews.Messages;

    static class WebApiHostBuilderExtensions
    {
        public static void UseWebApi(this IHostApplicationBuilder hostBuilder, List<Assembly> apiAssemblies, string rootUrl, bool startOwinHost)
        {
            //TODO Either use these or remove them
            _ = rootUrl;
            _ = startOwinHost;

            foreach (var apiAssembly in apiAssemblies)
            {
                hostBuilder.Services.RegisterApiTypes(apiAssembly);
                hostBuilder.Services.RegisterConcreteTypes(apiAssembly);
            }

            hostBuilder.Services.AddCors(options => options.AddDefaultPolicy(Cors.GetDefaultPolicy()));

            hostBuilder.Services.AddControllers();
            hostBuilder.Services.AddSignalR();
        }

        static void RegisterApiTypes(this IServiceCollection serviceCollection, Assembly assembly)
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

        static void RegisterConcreteTypes(this IServiceCollection serviceCollection, Assembly assembly)
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
