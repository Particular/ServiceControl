namespace Particular.ServiceControl
{
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using global::ServiceControl.LicenseManagement;
    using global::ServiceControl.Persistence;
    using global::ServiceControl.Transports;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Installers;
    using ServiceBus.Management.Infrastructure.Settings;

    class SetupBootstrapper
    {
        public SetupBootstrapper(Settings settings)
        {
            this.settings = settings;
        }

        public async Task Run()
        {
            // Validate license:
            if (!ValidateLicense(settings))
            {
                return;
            }

            var hostBuilder = Host.CreateApplicationBuilder();

            var componentSetupContext = new ComponentInstallationContext();

            foreach (ServiceControlComponent component in ServiceControlMainInstance.Components)
            {
                component.ConfigureInstallation(settings, hostBuilder);
                component.Setup(settings, componentSetupContext);
            }

            var persistence = PersistenceFactory.Create(settings);
            persistence.ConfigureInstaller(hostBuilder.Services);

            var host = hostBuilder.Build();

            await host.Services
                .GetRequiredService<IPersistenceInstaller>()
                .Install();

            foreach (var installationTask in componentSetupContext.InstallationTasks)
            {
                await installationTask(host.Services);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                EventSourceCreator.Create();
            }

            if (settings.SkipQueueCreation)
            {
                log.Info("Skipping queue creation");
            }
            else
            {
                var transportSettings = MapSettings(settings);
                var transportCustomization = settings.LoadTransportCustomization();

                await transportCustomization.ProvisionQueues(transportSettings,
                    componentSetupContext.Queues);
            }
        }

        bool ValidateLicense(Settings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.LicenseFileText))
            {
                if (!LicenseManager.IsLicenseValidForServiceControlInit(settings.LicenseFileText, out var errorMessageForLicenseText))
                {
                    log.Error(errorMessageForLicenseText);
                    return false;
                }

                if (!LicenseManager.TryImportLicenseFromText(settings.LicenseFileText, out var importErrorMessage))
                {
                    log.Error(importErrorMessage);
                    return false;
                }
            }
            else
            {
                var license = LicenseManager.FindLicense();
                if (!LicenseManager.IsLicenseValidForServiceControlInit(license, out var errorMessageForFoundLicense))
                {
                    log.Error(errorMessageForFoundLicense);
                    return false;
                }
            }

            return true;
        }

        static TransportSettings MapSettings(Settings settings)
        {
            var transportSettings = new TransportSettings
            {
                EndpointName = settings.ServiceName,
                ConnectionString = settings.TransportConnectionString,
                MaxConcurrency = settings.MaximumConcurrencyLevel
            };
            return transportSettings;
        }

        readonly Settings settings;
        static ILog log = LogManager.GetLogger<SetupBootstrapper>();
    }
}