namespace ServiceControl.Config.Commands
{
    using System;
    using Caliburn.Micro;
    using Events;
    using Framework;
    using Framework.Commands;
    using ServiceControlInstaller.Engine.Instances;
    using UI.InstanceDetails;
    using UI.InstanceEdit;

    class EditMonitoringInstanceCommand : AbstractCommand<InstanceDetailsViewModel>
    {
        public EditMonitoringInstanceCommand(IWindowManagerEx windowManager, Func<MonitoringInstance, MonitoringEditViewModel> editViewModel, IEventAggregator eventAggregator) : base(null)
        {
            this.windowManager = windowManager;
            this.editViewModel = editViewModel;
            this.eventAggregator = eventAggregator;
        }

        public override void Execute(InstanceDetailsViewModel viewModel)
        {
            var editVM = editViewModel((MonitoringInstance)viewModel.ServiceInstance);

            if (windowManager.ShowInnerDialog(editVM) ?? false)
            {
                editVM.UpdateInstanceFromViewModel((MonitoringInstance)viewModel.ServiceInstance);
                eventAggregator.PublishOnUIThread(new RefreshInstances());
            }
        }

        readonly Func<MonitoringInstance, MonitoringEditViewModel> editViewModel;
        readonly IEventAggregator eventAggregator;
        readonly IWindowManagerEx windowManager;
    }
}