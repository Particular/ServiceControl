namespace ServiceControl.Config.Commands
{
    using System;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using Events;
    using Framework;
    using Framework.Commands;
    using ServiceControl.Config.Framework.Modules;
    using ServiceControlInstaller.Engine.Instances;
    using UI.InstanceDetails;
    using UI.InstanceEdit;

    class EditMonitoringInstanceCommand : AwaitableAbstractCommand<InstanceDetailsViewModel>
    {
        public EditMonitoringInstanceCommand(
            IServiceControlWindowManager windowManager,
            Func<MonitoringInstance, MonitoringEditViewModel> editViewModel,
            IEventAggregator eventAggregator,
            MonitoringInstanceInstaller installer
            ) : base(null)
        {
            this.windowManager = windowManager;
            this.editViewModel = editViewModel;
            this.eventAggregator = eventAggregator;
            this.installer = installer;
        }

        public override async Task ExecuteAsync(InstanceDetailsViewModel viewModel)
        {
            var editVM = editViewModel((MonitoringInstance)viewModel.ServiceInstance);

            var instanceVersion = viewModel.Version;
            var installerVersion = installer.ZipInfo.Version;

            if (await InstallerVersionCompatibilityDialog.ShowValidation(instanceVersion, installerVersion, windowManager))
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
        readonly MonitoringInstanceInstaller installer;
    }
}