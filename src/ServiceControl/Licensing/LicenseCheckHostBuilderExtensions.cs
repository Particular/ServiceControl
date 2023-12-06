namespace Particular.ServiceControl.Licensing
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    static class LicenseCheckHostBuilderExtensions
    {
        public static IHostApplicationBuilder UseLicenseCheck(this IHostApplicationBuilder hostBuilder)
        {
            var services = hostBuilder.Services;
            services.AddSingleton<ActiveLicense>();
            services.AddHostedService<LicenseCheckHostedService>();
            return hostBuilder;
        }
    }
}