namespace ServiceControl.Config.Commands
{
    using System;
    using Framework;
    using Framework.Commands;
    using ServiceControlInstaller.Engine.Instances;
    using UI.AdvancedOptions;
    using UI.InstanceDetails;

    class AdvancedMonitoringOptionsCommand : AbstractCommand<InstanceDetailsViewModel>
    {
        public AdvancedMonitoringOptionsCommand(IWindowManagerEx windowManager, Func<BaseService, MonitoringAdvancedViewModel> advancedOptionsModel)
        {
            this.windowManager = windowManager;
            this.advancedOptionsModel = advancedOptionsModel;
        }

        public override void Execute(InstanceDetailsViewModel viewModel)
        {
            var screen = advancedOptionsModel(viewModel.MonitoringInstance);

            windowManager.ShowInnerDialog(screen);
        }

        readonly Func<BaseService, MonitoringAdvancedViewModel> advancedOptionsModel;
        readonly IWindowManagerEx windowManager;
    }
}