namespace ServiceControl.Config.UI.InstanceEdit
{
    using System;
    using System.ServiceProcess;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using Events;
    using Framework;
    using Framework.Modules;
    using ReactiveUI;
    using Validation;

    class ServiceControlEditAttachment : Attachment<ServiceControlEditViewModel>
    {
        public ServiceControlEditAttachment(IWindowManagerEx windowManager, IEventAggregator eventAggregator, ServiceControlInstanceInstaller installer)
        {
            this.windowManager = windowManager;
            this.installer = installer;
            this.eventAggregator = eventAggregator;
        }

        protected override void OnAttach()
        {
            var validationTemplate = new ValidationTemplate(viewModel);
            viewModel.ValidationTemplate = validationTemplate;

            viewModel.Save = ReactiveCommand.CreateFromTask(Save);
            viewModel.Cancel = Command.Create(() =>
            {
                viewModel.TryClose(false);
                eventAggregator.PublishOnUIThread(new RefreshInstances());
            }, IsInProgress);
        }

        bool IsInProgress()
        {
            return viewModel != null && !viewModel.InProgress;
        }

        async Task Save()
        {
            viewModel.SubmitAttempted = true;
            if (!viewModel.ValidationTemplate.Validate())
            {
                viewModel.NotifyOfPropertyChange(string.Empty);
                viewModel.SubmitAttempted = false;
                windowManager.ScrollFirstErrorIntoView(viewModel);
                return;
            }

            var instance = viewModel.ServiceControlInstance;
            if (instance.Service.Status == ServiceControllerStatus.Running)
            {
                if (!windowManager.ShowMessage("STOP INSTANCE AND MODIFY", $"{instance.Name} needs to be stopped in order to modify the settings. Do you want to proceed."))
                {
                    return;
                }
            }

            viewModel.InProgress = true;

            instance.LogPath = viewModel.ServiceControl.LogPath;
            instance.ServiceAccount = viewModel.ServiceControl.ServiceAccount;
            instance.ServiceAccountPwd = viewModel.ServiceControl.Password;
            instance.Description = viewModel.ServiceControl.Description;
            instance.HostName = viewModel.ServiceControl.HostName;
            instance.Port = Convert.ToInt32(viewModel.ServiceControl.PortNumber);
            instance.DatabaseMaintenancePort = !string.IsNullOrWhiteSpace(viewModel.ServiceControl.DatabaseMaintenancePortNumber) ? Convert.ToInt32(viewModel.ServiceControl.DatabaseMaintenancePortNumber) : (int?)null;
            instance.VirtualDirectory = null;
            instance.ForwardErrorMessages = viewModel.ServiceControl.ErrorForwarding.Value;
            instance.ErrorQueue = viewModel.ServiceControl.ErrorQueueName;
            instance.ErrorLogQueue = viewModel.ServiceControl.ErrorForwardingQueueName;
            instance.ErrorRetentionPeriod = viewModel.ServiceControl.ErrorRetentionPeriod;
            instance.EnableFullTextSearchOnBodies = viewModel.ServiceControl.EnableFullTextSearchOnBodies.Value;
            instance.TransportPackage = viewModel.SelectedTransport;
            instance.ConnectionString = viewModel.ConnectionString;

            using (var progress = viewModel.GetProgressObject("SAVING INSTANCE"))
            {
                progress.Report(0, 0, "Updating Instance");
                instance.Service.Refresh();
                var isRunning = instance.Service.Status == ServiceControllerStatus.Running;

                var reportCard = await Task.Run(() => installer.Update(instance, isRunning));

                if (reportCard.HasErrors || reportCard.HasWarnings)
                {
                    windowManager.ShowActionReport(reportCard, "ISSUES MODIFYING INSTANCE", "Could not modify instance because of the following errors:", "There were some warnings while modifying the instance:");
                    return;
                }

                progress.Report(0, 0, "Update Complete");
            }

            viewModel.TryClose(true);

            await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
        }

        readonly IWindowManagerEx windowManager;
        readonly IEventAggregator eventAggregator;
        readonly ServiceControlInstanceInstaller installer;
    }
}