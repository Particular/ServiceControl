namespace ServiceControl.Infrastructure.WebApi
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.CompositeViews.Messages;
    using Yarp.ReverseProxy.Forwarder;

    static class WebApiHostBuilderExtensions
    {
        public static void AddWebApi(this WebApplicationBuilder builder, List<Assembly> apiAssemblies, string rootUrl)
        {
            builder.WebHost.UseUrls(rootUrl);

            foreach (var apiAssembly in apiAssemblies)
            {
                builder.Services.RegisterApiTypes(apiAssembly);
                // TODO Why are we registering all concrete types? This blows up the ASP.Net Core DI container
                builder.Services.RegisterConcreteTypes(apiAssembly);
            }

            builder.Services.AddCors(options => options.AddDefaultPolicy(Cors.GetDefaultPolicy()));

            // We're not explicitly adding Gzip here because it's already in the default list of supported compressors
            builder.Services.AddResponseCompression();
            var controllers = builder.Services.AddControllers(options =>
            {
                options.Filters.Add<XParticularVersionHttpHandler>();
                options.Filters.Add<CachingHttpHandler>();
                options.Filters.Add<NotModifiedStatusHttpHandler>();

                options.ModelBinderProviders.Insert(0, new PagingInfoModelBindingProvider());
                options.ModelBinderProviders.Insert(0, new SortInfoModelBindingProvider());
            });
            controllers.AddJsonOptions(options => options.JsonSerializerOptions.CustomizeDefaults());

            builder.Services.AddHttpForwarder();

            var httpMessageInvoker = new HttpMessageInvoker(new SocketsHttpHandler()
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false,
                ActivityHeadersPropagator = new ReverseProxyPropagator(DistributedContextPropagator.Current),
                ConnectTimeout = TimeSpan.FromSeconds(15),
            });

            builder.Services.AddSingleton(typeof(HttpMessageInvoker), httpMessageInvoker);

            var signalR = builder.Services.AddSignalR();
            signalR.AddJsonProtocol(options => options.PayloadSerializerOptions.CustomizeDefaults());
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