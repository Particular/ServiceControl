namespace ServiceControl.Config.Commands
{
    using System.Threading.Tasks;
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Modules;
    using ServiceControlInstaller.Engine.Instances;

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
            if (licenseCheck)
            {
                var licenseCheckResult = serviceControlInstaller.CheckLicenseIsValid();
                if (!licenseCheckResult.Valid)
                {
                    await windowManager.ShowMessage("LICENSE ERROR", $"Upgrade could not continue due to an issue with the current license. {licenseCheckResult.Message}.  Contact contact@particular.net", hideCancel: true);
                    return false;
                }
            }

            var cantUpdateTransport = instance.TransportPackage.Removed && instance.TransportPackage.AutoMigrateTo is null;
            if (cantUpdateTransport)
            {
                await windowManager.ShowMessage("DEPRECATED MESSAGE TRANSPORT", $"The message transport '{instance.TransportPackage.DisplayName}' is not available in this version of ServiceControl, and this instance cannot be upgraded.", acceptText: "Cancel Upgrade", hideCancel: true);
                return false;
            }

            return true;
        }
    }
}
