namespace ServiceControl.Audit.Infrastructure.WebApi
{
    using System.Reflection;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using ServiceControl.Infrastructure;

    static class HostApplicationBuilderExtensions
    {
        public static void AddServiceControlAuditApi(this IHostApplicationBuilder builder, CorsSettings corsSettings)
        {
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
            controllers.AddJsonOptions(options => options.JsonSerializerOptions.CustomizeDefaults());
        }
    }
}