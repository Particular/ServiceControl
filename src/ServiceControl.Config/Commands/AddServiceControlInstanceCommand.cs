namespace ServiceControl.Config.Commands
{
    using System;
    using System.Threading.Tasks;
    using Framework;
    using Framework.Commands;
    using Framework.Modules;
    using UI.InstanceAdd;

    class AddServiceControlInstanceCommand : AwaitableAbstractCommand<object>
    {
        public AddServiceControlInstanceCommand(IServiceControlWindowManager windowManager, Func<ServiceControlAddViewModel> addInstance, ServiceControlInstanceInstaller installer) : base(null)
        {
            this.windowManager = windowManager;
            this.addInstance = addInstance;
            this.installer = installer;
        }

        [FeatureToggle(Feature.LicenseChecks)]
        public bool LicenseChecks { get; set; }

        public override async Task ExecuteAsync(object obj)
        {
            if (LicenseChecks)
            {
                var licenseCheckResult = installer.CheckLicenseIsValid();
                if (!licenseCheckResult.Valid)
                {
                    await windowManager.ShowMessage("LICENSE ERROR", $"Install could not continue due to an issue with the current license. {licenseCheckResult.Message}.  Contact contact@particular.net", hideCancel: true);
                    return;
                }
            }

            var instanceViewModel = addInstance();
            await windowManager.ShowInnerDialog(instanceViewModel);
        }

        readonly Func<ServiceControlAddViewModel> addInstance;
        readonly IServiceControlWindowManager windowManager;
        readonly ServiceControlInstanceInstaller installer;
    }
}