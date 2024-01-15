namespace Particular.ServiceControl.Commands
{
    using global::ServiceControl.Persistence;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Hosting.Internal;
    using Microsoft.Extensions.Hosting.WindowsServices;

    static class HostBuilderLifetimeExtensions
    {
        public static void SetupLifetime(this IHostBuilder hostBuilder, bool runAsWindowsService)
        {
            if (runAsWindowsService)
            {
                hostBuilder.UseWindowsService();
                hostBuilder.ConfigureServices(s =>
                {
                    s.AddSingleton<WindowsServiceLifetime>();
                    s.AddSingleton<IHostLifetime>(b => new PersisterInitializingLifetimeWrapper(b.GetRequiredService<IPersistenceLifecycle>(), b.GetRequiredService<WindowsServiceLifetime>()));
                });
            }
            else
            {
                hostBuilder.UseConsoleLifetime();
                hostBuilder.ConfigureServices(s =>
                {
                    s.AddSingleton<ConsoleLifetime>();
                    s.AddSingleton<IHostLifetime>(b => new PersisterInitializingLifetimeWrapper(b.GetRequiredService<IPersistenceLifecycle>(), b.GetRequiredService<ConsoleLifetime>()));
                });
            }
        }
    }
}