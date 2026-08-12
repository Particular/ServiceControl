namespace ServiceControl.Hosting.Commands
{
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Installers;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure;
    using ServiceControl.Persistence;
    using Transports;

    class SetupCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings, CancellationToken cancellationToken = default)
        {
            var hostBuilder = Host.CreateApplicationBuilder();
            hostBuilder.AddServiceControlInstallers(settings);

            var componentSetupContext = new ComponentInstallationContext();

            foreach (ServiceControlComponent component in ServiceControlMainInstance.Components)
            {
                component.Setup(settings, componentSetupContext, hostBuilder);
            }

            using IHost host = hostBuilder.Build();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                EventSourceCreator.Create();
            }

            await host.StartAsync(cancellationToken);

            if (args.SkipQueueCreation)
            {
                LoggerUtil.CreateStaticLogger<SetupCommand>().LogInformation("Skipping queue creation");
            }
            else
            {
                var transportSettings = settings.ToTransportSettings(componentSetupContext);
                transportSettings.RunCustomChecks = false;
                var transportCustomization = TransportFactory.Create(transportSettings);

                await transportCustomization.ProvisionQueues(transportSettings, componentSetupContext.Queues, cancellationToken);
            }

            await using (var scope = host.Services.CreateAsyncScope())
            {
                if (scope.ServiceProvider.GetService<IDatabaseMigrator>() is { } databaseMigrator)
                {
                    await databaseMigrator.ApplyMigrations(cancellationToken);
                }

                if (scope.ServiceProvider.GetService<IBodyStorageInstaller>() is { } bodyStorageInstaller)
                {
                    await bodyStorageInstaller.Provision(cancellationToken);
                }
            }

            await host.StopAsync(cancellationToken);
        }
    }
}