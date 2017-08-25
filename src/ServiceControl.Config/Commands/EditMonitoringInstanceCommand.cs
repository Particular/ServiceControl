namespace ServiceControl.Config.Commands
{
    using System;
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Commands;
    using ServiceControl.Config.UI.InstanceDetails;
    using ServiceControl.Config.UI.InstanceEdit;
    using ServiceControlInstaller.Engine.Instances;
    
    class EditMonitoringInstanceCommand : AbstractCommand<InstanceDetailsViewModel>
    {
        private readonly Func<MonitoringInstance, MonitoringEditViewModel> editViewModel;
        private readonly IWindowManagerEx windowManager;

        public EditMonitoringInstanceCommand(IWindowManagerEx windowManager, Func<MonitoringInstance, MonitoringEditViewModel> editViewModel) : base(null)
        {
            this.windowManager = windowManager;
            this.editViewModel = editViewModel;
        }

        public override void Execute(InstanceDetailsViewModel viewModel)
        {
            var editVM = editViewModel((MonitoringInstance)viewModel.ServiceInstance);

            windowManager.ShowInnerDialog(editVM);
        }
    }
}