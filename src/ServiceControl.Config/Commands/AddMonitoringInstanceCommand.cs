namespace ServiceControl.Config.Commands
{
    using System;
    using System.Threading.Tasks;
    using Framework;
    using Framework.Commands;
    using UI.InstanceAdd;

    class AddMonitoringInstanceCommand : AwaitableAbstractCommand<object>
    {
        public AddMonitoringInstanceCommand(IServiceControlWindowManager windowManager, Func<MonitoringAddViewModel> addInstance, CommandChecks commandChecks) : base(null)
        {
            this.windowManager = windowManager;
            this.addInstance = addInstance;
            this.commandChecks = commandChecks;
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