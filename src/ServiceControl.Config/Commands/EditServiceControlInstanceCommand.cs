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

    class EditServiceControlInstanceCommand : AbstractCommand<InstanceDetailsViewModel>
    {
        readonly Func<ServiceControlInstance, ServiceControlEditViewModel> editViewModel;
        readonly IEventAggregator eventAggregator;
        readonly IWindowManagerEx windowManager;

        public EditServiceControlInstanceCommand(IWindowManagerEx windowManager, Func<ServiceControlInstance, ServiceControlEditViewModel> editViewModel, IEventAggregator eventAggregator) : base(null)
        {
            this.windowManager = windowManager;
            this.editViewModel = editViewModel;
            this.eventAggregator = eventAggregator;
        }

        public override void Execute(InstanceDetailsViewModel viewModel)
        {
            var editVM = editViewModel((ServiceControlInstance)viewModel.ServiceInstance);

            windowManager.ShowInnerDialog(editVM);

            editVM.UpdateInstanceFromViewModel((ServiceControlInstance)viewModel.ServiceInstance);

            eventAggregator.PublishOnUIThread(new RefreshInstances());
        }
    }
}