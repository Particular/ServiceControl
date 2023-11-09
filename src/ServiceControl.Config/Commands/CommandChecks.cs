namespace ServiceControl.Config.Commands
{
    using System.Diagnostics;
    using System.Threading.Tasks;
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Modules;
    using ServiceControlInstaller.Engine;
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
            }

            return true;
        }
    }
}
