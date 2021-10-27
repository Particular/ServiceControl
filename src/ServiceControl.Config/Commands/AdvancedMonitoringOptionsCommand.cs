namespace ServiceControl.Config.Commands
{
    using System;
    using System.Threading.Tasks;
    using Framework;
    using Framework.Commands;
    using ServiceControlInstaller.Engine.Instances;
    using UI.AdvancedOptions;
    using UI.InstanceDetails;

    class AdvancedMonitoringOptionsCommand : AwaitableAbstractCommand<InstanceDetailsViewModel>
    {
        public AdvancedMonitoringOptionsCommand(IServiceControlWindowManager windowManager, Func<BaseService, MonitoringAdvancedViewModel> advancedOptionsModel)
        {
            this.windowManager = windowManager;
            this.advancedOptionsModel = advancedOptionsModel;
        }

        public override Task ExecuteAsync(InstanceDetailsViewModel viewModel)
        {
            var screen = advancedOptionsModel(viewModel.MonitoringInstance);

            return windowManager.ShowInnerDialog(screen);
        }

        readonly Func<BaseService, MonitoringAdvancedViewModel> advancedOptionsModel;
        readonly IServiceControlWindowManager windowManager;
    }
}