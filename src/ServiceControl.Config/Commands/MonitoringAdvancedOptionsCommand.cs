namespace ServiceControl.Config.Commands
{
    using System;
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Commands;
    using ServiceControl.Config.UI.AdvancedOptions;
    using ServiceControl.Config.UI.InstanceDetails;
    using ServiceControlInstaller.Engine.Instances;

    class MonitoringAdvancedOptionsCommand : AbstractCommand<InstanceDetailsViewModel>
    {
        private readonly Func<BaseService, MonitoringAdvancedViewModel> advancedOptionsModel;
        private readonly IWindowManagerEx windowManager;

        public MonitoringAdvancedOptionsCommand(IWindowManagerEx windowManager, Func<BaseService, MonitoringAdvancedViewModel> advancedOptionsModel)
        {
            this.windowManager = windowManager;
            this.advancedOptionsModel = advancedOptionsModel;
        }
        
        public override void Execute(InstanceDetailsViewModel viewModel)
        {
            var screen = advancedOptionsModel(viewModel.MonitoringInstance);

            windowManager.ShowInnerDialog(screen);
        }
    }
}