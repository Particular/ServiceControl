namespace ServiceControl.Hosting.Commands
{
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using LicenseManagement;
    using NServiceBus.Logging;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Hosting;
    using Persistence;
    using ServiceBus.Management.Infrastructure.Installers;
    using ServiceBus.Management.Infrastructure.Settings;
    using Transports;

    class SetupCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings)
        {
            settings.SkipQueueCreation = args.SkipQueueCreation;

            // Validate license:
            if (!ValidateLicense(settings))
            {
                return;
            }

            var componentSetupContext = new ComponentInstallationContext();

            foreach (ServiceControlComponent component in ServiceControlMainInstance.Components)
            {
                component.Setup(settings, componentSetupContext);
            }

            foreach (var installationTask in componentSetupContext.InstallationTasks)
            {
                await installationTask();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                EventSourceCreator.Create();
            }

            if (settings.SkipQueueCreation)
            {
                Logger.Info("Skipping queue creation");
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
                    Logger.Error(errorMessageForLicenseText);
                    return false;
                }

                if (!LicenseManager.TryImportLicenseFromText(settings.LicenseFileText, out var importErrorMessage))
                {
                    Logger.Error(importErrorMessage);
                    return false;
                }
            }
            else
            {
                var license = LicenseManager.FindLicense();
                if (!LicenseManager.IsLicenseValidForServiceControlInit(license, out var errorMessageForFoundLicense))
                {
                    Logger.Error(errorMessageForFoundLicense);
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

        static readonly ILog Logger = LogManager.GetLogger<SetupCommand>();
    }
}