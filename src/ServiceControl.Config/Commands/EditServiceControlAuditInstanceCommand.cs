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

    class EditServiceControlAuditInstanceCommand : AwaitableAbstractCommand<InstanceDetailsViewModel>
    {
        public EditServiceControlAuditInstanceCommand(
            IServiceControlWindowManager windowManager,
            Func<ServiceControlAuditInstance, ServiceControlAuditEditViewModel> editViewModel,
            IEventAggregator eventAggregator,
            ScmuCommandChecks commandChecks
        ) : base(CanEditInstance)
        {
            this.windowManager = windowManager;
            this.editViewModel = editViewModel;
            this.eventAggregator = eventAggregator;
            this.commandChecks = commandChecks;
        }

        static bool CanEditInstance(InstanceDetailsViewModel viewModel)
        {
            var instance = (ServiceControlAuditInstance)viewModel.ServiceInstance;
            return instance.VersionHasServiceControlAuditFeatures;
        }

        public override async Task ExecuteAsync(InstanceDetailsViewModel viewModel)
        {
            var editVM = editViewModel((ServiceControlAuditInstance)viewModel.ServiceInstance);

            if (!await commandChecks.CanEditInstance(viewModel.ServiceInstance))
            {
                return;
            }

            if (await windowManager.ShowInnerDialog(editVM) ?? false)
            {
                editVM.UpdateInstanceFromViewModel((ServiceControlAuditInstance)viewModel.ServiceInstance);
                await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
            }
        }

        readonly Func<ServiceControlAuditInstance, ServiceControlAuditEditViewModel> editViewModel;
        readonly IEventAggregator eventAggregator;
        readonly IServiceControlWindowManager windowManager;
        readonly ScmuCommandChecks commandChecks;
    }
}