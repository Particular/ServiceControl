namespace ServiceControl.Infrastructure.WebApi
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using ServiceControl.CompositeViews.Messages;

    static class WebApiHostBuilderExtensions
    {
        public static void AddWebApi(this WebApplicationBuilder builder, List<Assembly> apiAssemblies, string rootUrl)
        {
            builder.WebHost.UseUrls(rootUrl);

            foreach (var apiAssembly in apiAssemblies)
            {
                builder.Services.RegisterApiTypes(apiAssembly);
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
                serviceCollection.TryAddSingleton(apiType);
            }
        }
    }
}