namespace ServiceControl.Config.UI.InstanceEdit
{
    using System;
    using System.ServiceProcess;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using ReactiveUI;
    using ServiceControl.Config.Events;
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Modules;
    using Validation;

    class ServiceControlEditAttachment : Attachment<ServiceControlEditViewModel>
    {
        private readonly IWindowManagerEx windowManager;
        private readonly IEventAggregator eventAggregator;
        private readonly ServiceControlInstanceInstaller installer;

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

            viewModel.Save = new ReactiveCommand().DoAsync(Save);
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

        async Task Save(object arg)
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
            instance.LogPath = viewModel.LogPath;
            instance.ServiceAccount = viewModel.ServiceAccount;
            instance.ServiceAccountPwd = viewModel.Password;
            instance.Description = viewModel.Description;
            instance.HostName = viewModel.HostName;
            instance.Port = Convert.ToInt32(viewModel.PortNumber);
            instance.VirtualDirectory = null;
            instance.AuditLogQueue = viewModel.AuditForwardingQueueName;
            instance.AuditQueue = viewModel.AuditQueueName;
            instance.ForwardAuditMessages = viewModel.AuditForwarding.Value;
            instance.ForwardErrorMessages = viewModel.ErrorForwarding.Value;
            instance.ErrorQueue = viewModel.ErrorQueueName;
            instance.ErrorLogQueue = viewModel.ErrorForwardingQueueName;
            instance.AuditRetentionPeriod = viewModel.AuditRetentionPeriod;
            instance.ErrorRetentionPeriod = viewModel.ErrorRetentionPeriod;
            instance.TransportPackage = viewModel.SelectedTransport.Name;
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

            eventAggregator.PublishOnUIThread(new RefreshInstances());
        }
    }
}