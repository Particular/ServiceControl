using System;
using ServiceControl.Config.Framework;
using ServiceControl.Config.Framework.Commands;
using ServiceControl.Config.UI.InstanceAdd;

namespace ServiceControl.Config.Commands
{
    using ServiceControl.Config.Framework.Modules;

    class AddServiceControlInstanceCommand : AbstractCommand<object>
    {
        private readonly Func<ServiceControlAddViewModel> addInstance;
        private readonly IWindowManagerEx windowManager;
        private readonly ServiceControlInstanceInstaller installer;

        public AddServiceControlInstanceCommand(IWindowManagerEx windowManager, Func<ServiceControlAddViewModel> addInstance, ServiceControlInstanceInstaller installer) : base(null)
        {
            this.windowManager = windowManager;
            this.addInstance = addInstance;
            this.installer = installer;
        }

        public override void Execute(object obj)
        {
            if (LicenseChecks)
            {
                var licenseCheckResult = installer.CheckLicenseIsValid();
                if (!licenseCheckResult.Valid)
                {
                    windowManager.ShowMessage("LICENSE ERROR", $"Install could not continue due to an issue with the current license. {licenseCheckResult.Message}.  Contact contact@particular.net", hideCancel: true);
                    return;
                }
            }

            var instanceViewModel = addInstance();
            windowManager.ShowInnerDialog(instanceViewModel);
        }

        [FeatureToggle(Feature.LicenseChecks)]
        public bool LicenseChecks { get; set; }
    }
}