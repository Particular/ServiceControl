namespace ServiceControl.Config.UI.InstanceAdd
{
    using System;
    using System.Reactive.Linq;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using ReactiveUI;
    using ServiceControl.Config.Events;
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Modules;
    using ServiceControlInstaller.Engine.Instances;
    using Validation;

    internal class InstanceAddAttachment : Attachment<InstanceAddViewModel>
    {
        private readonly IWindowManagerEx windowManager;
        private readonly IEventAggregator eventAggregator;
        private readonly Installer installer;

        public InstanceAddAttachment(IWindowManagerEx windowManager, IEventAggregator eventAggregator, Installer installer)
        {
            this.windowManager = windowManager;
            this.installer = installer;
            this.eventAggregator = eventAggregator;
        }

        protected override void OnAttach()
        {
            var validationTemplate = new ValidationTemplate(viewModel);
            viewModel.ValidationTemplate = validationTemplate;

            viewModel.Save = new ReactiveCommand(validationTemplate.ErrorsChangedObservable.Select(_ => !validationTemplate.HasErrors).DistinctUntilChanged())
                .DoAsync(Add);
            viewModel.Cancel = Command.Create(() => viewModel.TryClose(false), IsInProgress);
        }

        bool IsInProgress()
        {
            return viewModel != null && !viewModel.InProgress;
        }

        private async Task Add(object arg)
        {
            if (!viewModel.ValidationTemplate.Validate())
            {
                viewModel.NotifyOfPropertyChange(string.Empty);
                return;
            }

            viewModel.InProgress = true;

            var instanceMetadata = new ServiceControlInstanceMetadata
            {
                DisplayName = viewModel.InstanceName,
                Name = viewModel.InstanceName.Replace(' ', '.'),
                ServiceDescription = viewModel.Description,
                DBPath = viewModel.DatabasePath,
                LogPath = viewModel.LogPath,
                InstallPath = viewModel.DestinationPath,
                HostName = viewModel.HostName,
                Port = Convert.ToInt32(viewModel.PortNumber),
                VirtualDirectory = null, // TODO
                AuditLogQueue = viewModel.AuditForwardingQueueName,
                AuditQueue = viewModel.AuditQueueName,
                ForwardAuditMessages = viewModel.AuditForwarding.Value,
                ErrorQueue = viewModel.ErrorQueueName,
                ErrorLogQueue = viewModel.ErrorForwardingQueueName,
                TransportPackage = viewModel.SelectedTransport.Name,
                ConnectionString = viewModel.ConnectionString,
                ServiceAccount = viewModel.ServiceAccount,
                ServiceAccountPwd = viewModel.Password
            };

            using (var progress = viewModel.GetProgressObject("ADDING INSTANCE"))
            {
                var reportCard = await Task.Run(() => installer.Add(instanceMetadata, progress));

                if (reportCard.HasErrors || reportCard.HasWarnings)
                {
                    windowManager.ShowActionReport(reportCard, "ISSUES ADDING INSTANCE", "Could not add new instance because of the following errors:", "There were some warnings while adding the instance:");
                }
            }

            viewModel.TryClose(true);

            eventAggregator.PublishOnUIThread(new RefreshInstances());
        }
    }
}