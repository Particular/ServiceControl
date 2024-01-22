namespace Microsoft.Extensions.Hosting
{
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Hosting;

    static class HostBuilderLifetimeExtensions
    {
        public static void AddPersistenceInitializingLifetime(this IHostBuilder hostBuilder, bool runAsWindowsService)
        {
            if (runAsWindowsService)
            {
                hostBuilder.UseWindowsService();

                hostBuilder.ConfigureServices(s =>
                {
                    s.AddSingleton<IHostLifetime, PersisterInitializingWindowsServiceLifetime>();
                });
            }
            else
            {
                hostBuilder.UseConsoleLifetime();

                hostBuilder.ConfigureServices(s =>
                {
                    s.AddSingleton<IHostLifetime, PersisterInitializingConsoleLifetime>();
                });
            }
        }
    }
}