namespace Particular.ServiceControl.Licensing
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    static class LicenseCheckHostBuilderExtensions
    {
        public static IHostBuilder UseLicenseCheck(this IHostBuilder hostBuilder)
        {
            hostBuilder.ConfigureServices(collection =>
            {
                collection.AddSingleton<ActiveLicense>();
                collection.AddHostedService<LicenseCheckHostedService>();
            });
            return hostBuilder;
        }
    }
}