namespace ServiceControl.Config.Commands
{
    using System;
    using Caliburn.Micro;
    using ServiceControl.Config.Events;
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Commands;
    using ServiceControl.Config.UI.InstanceDetails;
    using ServiceControl.Config.UI.InstanceEdit;
    using ServiceControlInstaller.Engine.Instances;

    class EditServiceControlInstanceCommand : AbstractCommand<InstanceDetailsViewModel>
    {
        private readonly Func<ServiceControlInstance, ServiceControlEditViewModel> editViewModel;
        private readonly IEventAggregator eventAggregator;
        private readonly IWindowManagerEx windowManager;

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