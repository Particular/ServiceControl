namespace ServiceControl.Config.Commands
{
    using System;
    using Framework;
    using Framework.Commands;
    using ServiceControlInstaller.Engine.Instances;
    using UI.InstanceDetails;
    using UI.InstanceEdit;

    class EditMonitoringInstanceCommand : AbstractCommand<InstanceDetailsViewModel>
    {
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

        readonly Func<MonitoringInstance, MonitoringEditViewModel> editViewModel;
        readonly IWindowManagerEx windowManager;
    }
}