namespace ServiceControl.Audit.Infrastructure.WebApi
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.DependencyInjection;

    static class WebApplicationBuilderExtensions
    {
        public static void AddWebApi(this WebApplicationBuilder builder, string rootUrl)
        {
            builder.WebHost.UseUrls(rootUrl);

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
        }
    }
}