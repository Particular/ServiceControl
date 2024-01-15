namespace Particular.ServiceControl.Commands
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    static class HostBuilderLifetimeExtensions
    {
        public static void SetupLifetime(this IHostBuilder hostBuilder, bool runAsWindowsService)
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