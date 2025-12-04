namespace ServiceControl.Infrastructure.WebApi
{
    using System.Linq;
    using System.Reflection;
    using CompositeViews.Messages;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Hosting;
    using Particular.LicensingComponent.WebApi;
    using Particular.ServiceControl;

    static class HostApplicationBuilderExtensions
    {
        public static void AddServiceControlApi(this IHostApplicationBuilder builder, CorsSettings corsSettings)
        {
            // This registers concrete classes that implement IApi. Currently it is hard to find out to what
            // component those APIs should belong to so we leave it here for now.
            builder.Services.RegisterApiTypes(Assembly.GetExecutingAssembly());

            builder.AddServiceControlApis();

            builder.Services.AddCors(options => options.AddDefaultPolicy(Cors.GetDefaultPolicy(corsSettings)));

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
            controllers.AddApplicationPart(Assembly.GetExecutingAssembly());
            controllers.AddApplicationPart(typeof(LicensingController).Assembly);
            controllers.AddJsonOptions(options => options.JsonSerializerOptions.CustomizeDefaults());

            var signalR = builder.Services.AddSignalR();
            signalR.AddJsonProtocol(options => options.PayloadSerializerOptions.CustomizeDefaults());
        }

        static void RegisterApiTypes(this IServiceCollection serviceCollection, Assembly assembly)
        {
            var apiTypes = assembly.DefinedTypes
                .Where(ti =>
                             ti is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false } &&
                             ti.GetInterfaces().Any(t => t == typeof(IApi)))
                .Select(ti => ti.AsType());


            foreach (var apiType in apiTypes)
            {
                serviceCollection.TryAddSingleton(apiType);
            }
        }
    }
}