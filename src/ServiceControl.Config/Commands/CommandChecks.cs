namespace ServiceControl.Config.Commands
{
    using System.Diagnostics;
    using System.Threading.Tasks;
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Modules;
    using ServiceControl.Engine.Extensions;
    using ServiceControlInstaller.Engine;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Validation;

    public class CommandChecks
    {
        readonly ServiceControlInstanceInstaller serviceControlInstaller;
        readonly IServiceControlWindowManager windowManager;

        public CommandChecks(ServiceControlInstanceInstaller serviceControlInstaller, IServiceControlWindowManager windowManager)
        {
            this.serviceControlInstaller = serviceControlInstaller;
            this.windowManager = windowManager;
        }

        public async Task<bool> CanUpgradeInstance(BaseService instance, bool licenseCheck)
        {
            // Check for license
            if (licenseCheck)
            {
                var licenseCheckResult = serviceControlInstaller.CheckLicenseIsValid();
                if (!licenseCheckResult.Valid)
                {
                    await windowManager.ShowMessage("LICENSE ERROR", $"Upgrade could not continue due to an issue with the current license. {licenseCheckResult.Message}.  Contact contact@particular.net", hideCancel: true);
                    return false;
                }
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
            if (DotnetVersionValidator.FrameworkRequirementsAreMissing(needsRavenDB, out var missingMessage))
            {
                await windowManager.ShowMessage("Missing prerequisites", missingMessage, acceptText: "Cancel", hideCancel: true);
                return false;
            }

            // RavenDB 5+ check
            if (instance is IServiceControlBaseInstance baseInstance)
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

                // TODO: Why doesn't the Monitoring instance do this check? Should it?
                var upgradeInfo = UpgradeInfo.GetUpgradePathFor(instance.Version);
                if (upgradeInfo.HasIncompatibleVersion)
                {
                    var nextVersion = upgradeInfo.UpgradePath[0];
                    await windowManager.ShowMessage("VERSION UPGRADE INCOMPATIBLE",
                        "<Section xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xml:space=\"preserve\" TextAlignment=\"Left\" LineHeight=\"Auto\" IsHyphenationEnabled=\"False\" xml:lang=\"en-us\">\r\n" +
                        $"<Paragraph>You must upgrade to version(s) {upgradeInfo} before upgrading to version {serviceControlInstaller.ZipInfo.Version}:</Paragraph>\r\n" +
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
                !await windowManager.ShowYesNoDialog("UPGRADE WARNING", $"ServiceControl version {serviceControlInstaller.ZipInfo.Version} requires RabbitMQ broker version 3.10.0 or higher. Also, the stream_queue and quorum_queue feature flags must be enabled on the broker. Please confirm your broker meets the minimum requirements before upgrading.",
                   "Do you want to proceed?",
                   "Yes, my RabbitMQ broker meets the minimum requirements",
                   "No, cancel the upgrade"))
            {
                return false;
            }

            return true;
        }
    }
}
