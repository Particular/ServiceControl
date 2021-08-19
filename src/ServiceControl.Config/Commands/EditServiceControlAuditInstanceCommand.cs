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

    class EditServiceControlAuditInstanceCommand : AbstractCommand<InstanceDetailsViewModel>
    {
        public EditServiceControlAuditInstanceCommand(IServiceControlWindowManager windowManager, Func<ServiceControlAuditInstance, ServiceControlAuditEditViewModel> editViewModel, IEventAggregator eventAggregator) : base(CanEditInstance)
        {
            this.windowManager = windowManager;
            this.editViewModel = editViewModel;
            this.eventAggregator = eventAggregator;
        }

        static bool CanEditInstance(InstanceDetailsViewModel viewModel)
        {
            var instance = (ServiceControlAuditInstance)viewModel.ServiceInstance;
            return instance.VersionHasServiceControlAuditFeatures;
        }

        public override void Execute(InstanceDetailsViewModel viewModel)
        {
            var editVM = editViewModel((ServiceControlAuditInstance)viewModel.ServiceInstance);

            if (windowManager.ShowInnerDialog(editVM) ?? false)
            {
                editVM.UpdateInstanceFromViewModel((ServiceControlAuditInstance)viewModel.ServiceInstance);
                eventAggregator.PublishOnUIThread(new RefreshInstances());
            }
        }

        readonly Func<ServiceControlAuditInstance, ServiceControlAuditEditViewModel> editViewModel;
        readonly IEventAggregator eventAggregator;
        readonly IServiceControlWindowManager windowManager;
    }
}