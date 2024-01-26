namespace ServiceControl.Audit.Infrastructure.WebApi
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using OWIN;

    static class WebApiHostBuilderExtensions
    {
        public static IHostBuilder UseWebApi(this IHostBuilder hostBuilder, string rootUrl, bool startOwinHost)
        {
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
    }
}