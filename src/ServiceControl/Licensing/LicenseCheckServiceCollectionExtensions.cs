namespace Particular.ServiceControl.Licensing
{
    using Microsoft.Extensions.DependencyInjection;

    static class LicenseCheckServiceCollectionExtensions
    {
        public static void AddLicenseCheck(this IServiceCollection services)
        {
            services.AddSingleton<ActiveLicense>();
            services.AddHostedService<LicenseCheckHostedService>();
        }
    }
}