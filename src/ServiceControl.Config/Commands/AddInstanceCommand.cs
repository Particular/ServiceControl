using System;
using ServiceControl.Config.Framework;
using ServiceControl.Config.Framework.Commands;
using ServiceControl.Config.UI.InstanceAdd;

namespace ServiceControl.Config.Commands
{
    using ServiceControl.Config.Framework.Modules;

    class AddInstanceCommand : AbstractCommand<object>
    {
        private readonly Func<InstanceAddViewModel> addInstance;
        private readonly IWindowManagerEx windowManager;
        private readonly ServiceControlInstanceInstaller installer;

        public AddInstanceCommand(IWindowManagerEx windowManager, Func<InstanceAddViewModel> addInstance, ServiceControlInstanceInstaller installer) : base(null)
        {
            this.windowManager = windowManager;
            this.addInstance = addInstance;
            this.installer = installer;
        }

        public override void Execute(object obj)
        {
            var licenseCheckResult = installer.CheckLicenseIsValid();
            if (!licenseCheckResult.Valid)
            {
                windowManager.ShowMessage("LICENSE ERROR", $"Install could not continue due to an issue with the current license. {licenseCheckResult.Message}.  Contact sales@particular.net", hideCancel: true);
                return;
            }

            var instanceViewModel = addInstance();
            windowManager.ShowInnerDialog(instanceViewModel);
        }
    }
}