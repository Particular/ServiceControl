namespace Particular.ServiceControl
{
    using System;
    using System.Threading.Tasks;
    using global::ServiceControl.Infrastructure.RavenDB;
    using Hosting;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure.Settings;

    static class MaintenanceBootstrapper
    {
        public static async Task Run(HostArguments args)
        {
            var settings = new Settings(args.ServiceName);

            var hostBuilder = new HostBuilder()
                .ConfigureServices(services =>
                {
                    var documentStore = new EmbeddableDocumentStore();

                    RavenBootstrapper.Configure(documentStore, settings, true);

                    services.AddHostedService(sp => new EmbeddedRavenDbHostedService(documentStore, new IDataMigration[0], new ComponentInstallationContext()));
                });

            if (args.RunAsWindowsService)
            {
                hostBuilder.UseWindowsService();

                await hostBuilder.Build().RunAsync().ConfigureAwait(false);
            }
            else
            {
                await Console.Out.WriteLineAsync($"RavenDB is now accepting requests on {settings.DatabaseMaintenanceUrl}").ConfigureAwait(false);
                await Console.Out.WriteLineAsync("RavenDB Maintenance Mode - Press CTRL+C to exit").ConfigureAwait(false);

                hostBuilder.UseConsoleLifetime();

                await hostBuilder.Build().RunAsync().ConfigureAwait(false);
            }
        }
    }
}