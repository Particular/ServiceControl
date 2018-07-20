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

    class MonitoringEditAttachment : Attachment<MonitoringEditViewModel>
    {
        public MonitoringEditAttachment(IWindowManagerEx windowManager, IEventAggregator eventAggregator, MonitoringInstanceInstaller installer)
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

            var instance = viewModel.MonitoringInstance;
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
            instance.ErrorQueue = viewModel.ErrorQueueName;
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

            eventAggregator.PublishOnUIThread(new RefreshInstances());
        }

        readonly IWindowManagerEx windowManager;
        readonly IEventAggregator eventAggregator;
        readonly MonitoringInstanceInstaller installer;
    }
}