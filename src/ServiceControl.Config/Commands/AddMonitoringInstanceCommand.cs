namespace ServiceControl.Config.Commands
{
    using System;
    using System.Threading.Tasks;
    using Framework;
    using Framework.Commands;
    using Framework.Modules;
    using UI.InstanceAdd;

    class AddMonitoringInstanceCommand : AwaitableAbstractCommand<object>
    {
        public AddMonitoringInstanceCommand(IServiceControlWindowManager windowManager, Func<MonitoringAddViewModel> addInstance, MonitoringInstanceInstaller installer) : base(null)
        {
            this.windowManager = windowManager;
            this.addInstance = addInstance;
            commandChecks = new CommandChecks(installer, windowManager);
        }

        [FeatureToggle(Feature.LicenseChecks)]
        public bool LicenseChecks { get; set; }

        public override async Task ExecuteAsync(object obj)
        {
            if (!await commandChecks.CanAddInstance(LicenseChecks))
            {
                return;
            }

            var instanceViewModel = addInstance();
            await windowManager.ShowInnerDialog(instanceViewModel);
        }

        readonly Func<MonitoringAddViewModel> addInstance;
        readonly IServiceControlWindowManager windowManager;
        readonly CommandChecks commandChecks;
    }
}