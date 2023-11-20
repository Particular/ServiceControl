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

    class EditServiceControlInstanceCommand : AwaitableAbstractCommand<InstanceDetailsViewModel>
    {
        public EditServiceControlInstanceCommand(
            IServiceControlWindowManager windowManager,
            Func<ServiceControlInstance, ServiceControlEditViewModel> editViewModel,
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
            var instance = (ServiceControlInstance)viewModel.ServiceInstance;
            return instance.VersionHasServiceControlAuditFeatures;
        }

        public override async Task ExecuteAsync(InstanceDetailsViewModel viewModel)
        {
            var editVM = editViewModel((ServiceControlInstance)viewModel.ServiceInstance);

            if (!await commandChecks.CanEditInstance(viewModel.ServiceInstance))
            {
                return;
            }

            if (await windowManager.ShowInnerDialog(editVM) ?? false)
            {
                editVM.UpdateInstanceFromViewModel((ServiceControlInstance)viewModel.ServiceInstance);
                await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
            }
        }

        readonly Func<ServiceControlInstance, ServiceControlEditViewModel> editViewModel;
        readonly IEventAggregator eventAggregator;
        readonly IServiceControlWindowManager windowManager;
        readonly ScmuCommandChecks commandChecks;
    }
}