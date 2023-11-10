namespace ServiceControl.Config.Commands
{
    using System.Diagnostics;
    using System.Linq;
    using System.ServiceProcess;
    using System.Threading.Tasks;
    using ServiceControl.Config.Framework;
    using ServiceControl.Engine.Extensions;
    using ServiceControl.LicenseManagement;
    using ServiceControlInstaller.Engine;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Validation;

    public class CommandChecks
    {
        readonly IServiceControlWindowManager windowManager;

        public CommandChecks(IServiceControlWindowManager windowManager)
        {
            this.windowManager = windowManager;
        }

        public async Task<bool> CanAddInstance(bool needsRavenDB)
        {
            // Check for license
            if (!await IsLicenseOk())
            {
                return false;
            }

            if (await FrameworkRequirementsAreMissing(needsRavenDB))
            {
                return false;
            }

            return true;
        }

        public async Task<bool> ValidateNewInstance(params IServiceInstance[] instances)
        {
            var transport = instances
                .OfType<ITransportConfig>()
                .Where(i => i is not null)
                .Select(i => i.TransportPackage)
                .First(t => t is not null);

            if (transport is not null)
            {
                if (transport.IsLatestRabbitMQTransport())
                {
                    var continueInstall = await windowManager.ShowYesNoDialog("INSTALL WARNING", $"ServiceControl version {Constants.CurrentVersion} requires RabbitMQ broker version 3.10.0 or higher. Also, the stream_queue and quorum_queue feature flags must be enabled on the broker. Please confirm your broker meets the minimum requirements before installing.",
                                                     "Do you want to proceed?",
                                                     "Yes, my RabbitMQ broker meets the minimum requirements",
                                                     "No, cancel the install");

                    if (!continueInstall)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public async Task<bool> CanUpgradeInstance(BaseService instance, bool forceUpgradeDb = false)
        {
            // Check for license
            if (!await IsLicenseOk())
            {
                return false;
            }

            // Check for transports that can't be upgraded
            var cantUpdateTransport = instance.TransportPackage.Removed && instance.TransportPackage.AutoMigrateTo is null;
            if (cantUpdateTransport)
            {
                await windowManager.ShowMessage("DEPRECATED MESSAGE TRANSPORT", $"The message transport '{instance.TransportPackage.DisplayName}' is not available in this version of ServiceControl, and this instance cannot be upgraded.", acceptText: "Cancel Upgrade", hideCancel: true);
                return false;
            }

            // Validate .NET Framework requirements
            bool needsRavenDB = instance is IServiceControlBaseInstance;
            if (await FrameworkRequirementsAreMissing(needsRavenDB))
            {
                return false;
            }

            if (DotnetVersionValidator.FrameworkRequirementsAreMissing(needsRavenDB, out var missingMessage))
            {
                await windowManager.ShowMessage("Missing prerequisites", missingMessage, acceptText: "Cancel", hideCancel: true);
                return false;
            }

            // RavenDB 5+ check
            if (instance is IServiceControlBaseInstance baseInstance)
            {
                if (!forceUpgradeDb)
                {
                    var compatibleStorageEngine = baseInstance.PersistenceManifest.Name == StorageEngineNames.RavenDB;

                    if (!compatibleStorageEngine)
                    {
                        var upgradeGuide4to5url = "https://docs.particular.net/servicecontrol/upgrades/4to5/";

                        var openUpgradeGuide = await windowManager.ShowYesNoDialog("STORAGE ENGINE INCOMPATIBLE",
                            $"Please note that the storage format has changed and the {baseInstance.PersistenceManifest.DisplayName} storage engine is no longer available. Upgrading requires a side-by-side deployment of both versions. Migration guidance is available in the version 4 to 5 upgrade guidance at {upgradeGuide4to5url}",
                            "Open online ServiceControl 4 to 5 upgrade guide in system default browser?",
                            "Yes",
                            "No"
                        );

                        if (openUpgradeGuide)
                        {
                            Process.Start(new ProcessStartInfo(upgradeGuide4to5url) { UseShellExecute = true });
                        }

                        return false;
                    }
                }

                // TODO: Why doesn't the Monitoring instance do this check? Should it?
                var upgradeInfo = UpgradeInfo.GetUpgradePathFor(instance.Version);
                if (upgradeInfo.HasIncompatibleVersion)
                {
                    var nextVersion = upgradeInfo.UpgradePath[0];
                    await windowManager.ShowMessage("VERSION UPGRADE INCOMPATIBLE",
                        "<Section xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xml:space=\"preserve\" TextAlignment=\"Left\" LineHeight=\"Auto\" IsHyphenationEnabled=\"False\" xml:lang=\"en-us\">\r\n" +
                        $"<Paragraph>You must upgrade to version(s) {upgradeInfo} before upgrading to version {Constants.CurrentVersion}:</Paragraph>\r\n" +
                        "<List MarkerStyle=\"Decimal\" Margin=\"0,0,0,0\" Padding=\"0,0,0,0\">\r\n" +
                        $"<ListItem Margin=\"48,0,0,0\"><Paragraph>Download and install version {nextVersion} from https://github.com/Particular/ServiceControl/releases/tag/{nextVersion}</Paragraph></ListItem>" +
                        $"<ListItem Margin=\"48,0,0,0\"><Paragraph>Upgrade this instance to version {nextVersion}.</Paragraph></ListItem>\r\n" +
                        "<ListItem Margin=\"48,0,0,0\"><Paragraph>Download and install the latest version from https://particular.net/start-servicecontrol-download</Paragraph></ListItem>\r\n" +
                        "<ListItem Margin=\"48,0,0,0\"><Paragraph>Upgrade this instance to the latest version of ServiceControl.</Paragraph></ListItem>\r\n" +
                        "</List>\r\n" +
                        "</Section>",
                        hideCancel: true);

                    return false;
                }
            }

            if (instance.TransportPackage.IsOldRabbitMQTransport() &&
                !await windowManager.ShowYesNoDialog("UPGRADE WARNING", $"ServiceControl version {Constants.CurrentVersion} requires RabbitMQ broker version 3.10.0 or higher. Also, the stream_queue and quorum_queue feature flags must be enabled on the broker. Please confirm your broker meets the minimum requirements before upgrading.",
                   "Do you want to proceed?",
                   "Yes, my RabbitMQ broker meets the minimum requirements",
                   "No, cancel the upgrade"))
            {
                return false;
            }

            return true;
        }

        async Task<bool> FrameworkRequirementsAreMissing(bool needsRavenDB)
        {
            if (DotnetVersionValidator.FrameworkRequirementsAreMissing(needsRavenDB, out var missingMessage))
            {
                await windowManager.ShowMessage("Missing prerequisites", missingMessage, acceptText: "Cancel", hideCancel: true);
                return true;
            }

            return false;
        }

        async Task<bool> IsLicenseOk()
        {
            var licenseCheckResult = CheckLicenseIsValid();
            if (!licenseCheckResult.Valid)
            {
                await windowManager.ShowMessage("LICENSE ERROR", $"Upgrade could not continue due to an issue with the current license. {licenseCheckResult.Message}.  Contact contact@particular.net", hideCancel: true);
                return false;
            }

            return true;
        }

        public async Task<bool> StopBecauseInstanceIsRunning(BaseService instance, string instanceName)
        {
            if (instance.Service.Status == ServiceControllerStatus.Stopped)
            {
                return false;
            }

            var proceed = await windowManager.ShowYesNoDialog($"STOP INSTANCE AND UPGRADE TO {Constants.CurrentVersion}",
                $"{instanceName} needs to be stopped in order to upgrade to version {Constants.CurrentVersion}.",
                "Do you want to proceed?",
                "Yes, I want to proceed", "No");

            return !proceed;
        }

        CheckLicenseResult CheckLicenseIsValid()
        {
            var license = LicenseManager.FindLicense();

            if (license.Details.HasLicenseExpired())
            {
                return new CheckLicenseResult(false, "License has expired");
            }

            if (!license.Details.ValidForServiceControl)
            {
                return new CheckLicenseResult(false, "This license edition does not include ServiceControl");
            }

            var releaseDate = LicenseManager.GetReleaseDate();

            if (license.Details.ReleaseNotCoveredByMaintenance(releaseDate))
            {
                return new CheckLicenseResult(false, "License does not cover this release of ServiceControl Monitoring. Upgrade protection expired.");
            }

            return new CheckLicenseResult(true);
        }

        class CheckLicenseResult
        {
            public CheckLicenseResult(bool valid, string message = null)
            {
                Valid = valid;
                Message = message;
            }

            public bool Valid { get; private set; }
            public string Message { get; private set; }
        }
    }
}
