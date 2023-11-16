namespace ServiceControl.Config.Commands
{
    using System;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using Events;
    using Framework;
    using Framework.Commands;
    using ServiceControlInstaller.Engine.Instances;
    using UI.InstanceDetails;
    using UI.InstanceEdit;

    class EditMonitoringInstanceCommand : AwaitableAbstractCommand<InstanceDetailsViewModel>
    {
        public EditMonitoringInstanceCommand(
            IServiceControlWindowManager windowManager,
            Func<MonitoringInstance, MonitoringEditViewModel> editViewModel,
            IEventAggregator eventAggregator,
            ScmuCommandChecks commandChecks
            ) : base(null)
        {
            this.windowManager = windowManager;
            this.editViewModel = editViewModel;
            this.eventAggregator = eventAggregator;
            this.commandChecks = commandChecks;
        }

        public override async Task ExecuteAsync(InstanceDetailsViewModel viewModel)
        {
            var editVM = editViewModel((MonitoringInstance)viewModel.ServiceInstance);

            if (!await commandChecks.CanEditInstance(viewModel.ServiceInstance))
            {
                return;
            }

            if (await windowManager.ShowInnerDialog(editVM) ?? false)
            {
                editVM.UpdateInstanceFromViewModel((MonitoringInstance)viewModel.ServiceInstance);
                await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
            }
        }

        readonly Func<MonitoringInstance, MonitoringEditViewModel> editViewModel;
        readonly IEventAggregator eventAggregator;
        readonly IServiceControlWindowManager windowManager;
        readonly ScmuCommandChecks commandChecks;
    }
}