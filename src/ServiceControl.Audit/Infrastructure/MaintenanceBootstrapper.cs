namespace ServiceControl.Audit.Infrastructure
{
    using System;
    using System.Threading.Tasks;
    using Hosting;
    using Microsoft.Extensions.Hosting;
    using ServiceControl.Audit.Persistence;

    static class MaintenanceBootstrapper
    {
        public static async Task Run(HostArguments args)
        {
            var settings = new Settings.Settings(args.ServiceName);

            var hostBuilder = new HostBuilder()
                .SetupPersistence(settings, true);

            if (args.RunAsWindowsService)
            {
                hostBuilder.UseWindowsService();

                await hostBuilder.Build().RunAsync().ConfigureAwait(false);
            }
            else
            {
                hostBuilder.UseConsoleLifetime();

                await Console.Out.WriteLineAsync($"RavenDB is now accepting requests on {settings.DatabaseMaintenanceUrl}")
                    .ConfigureAwait(false);
                await Console.Out.WriteLineAsync("RavenDB Maintenance Mode - Press CTRL+C to exit")
                    .ConfigureAwait(false);

                await hostBuilder.Build().RunAsync().ConfigureAwait(false);

                await Console.Out.WriteLineAsync("Disposing RavenDB document store (this might take a while)...")
                    .ConfigureAwait(false);
            }
        }
    }
}