﻿namespace ServiceControl.Config.Commands
{
    using System;
    using Framework;
    using Framework.Commands;
    using Framework.Modules;
    using UI.InstanceAdd;

    class AddMonitoringInstanceCommand : AbstractCommand<object>
    {
        public AddMonitoringInstanceCommand(IServiceControlWindowManager windowManager, Func<MonitoringAddViewModel> addInstance, MonitoringInstanceInstaller installer) : base(null)
        {
            this.windowManager = windowManager;
            this.addInstance = addInstance;
            this.installer = installer;
        }

        [FeatureToggle(Feature.LicenseChecks)]
        public bool LicenseChecks { get; set; }

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

        readonly Func<MonitoringAddViewModel> addInstance;
        readonly IServiceControlWindowManager windowManager;
        readonly MonitoringInstanceInstaller installer;
    }
}