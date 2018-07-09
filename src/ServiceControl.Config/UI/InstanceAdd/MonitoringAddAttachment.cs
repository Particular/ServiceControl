namespace ServiceControl.Config.UI.InstanceAdd
{
    using System;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using ReactiveUI;
    using ServiceControl.Config.Events;
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Modules;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Validation;
    using Validation;

    internal class MonitoringAddAttachment : Attachment<MonitoringAddViewModel>
    {
        private readonly IWindowManagerEx windowManager;
        private readonly IEventAggregator eventAggregator;
        private readonly MonitoringInstanceInstaller installer;

        public MonitoringAddAttachment(IWindowManagerEx windowManager, IEventAggregator eventAggregator, MonitoringInstanceInstaller installer)
        {
            this.windowManager = windowManager;
            this.installer = installer;
            this.eventAggregator = eventAggregator;
        }

        protected override void OnAttach()
        {
            var validationTemplate = new ValidationTemplate(viewModel);
            viewModel.ValidationTemplate = validationTemplate;

            viewModel.Save = new ReactiveCommand().DoAsync(Add);
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

        private async Task Add(object arg)
        {
            viewModel.SubmitAttempted = true;
            if (!viewModel.ValidationTemplate.Validate())
            {
                viewModel.NotifyOfPropertyChange(string.Empty);
                viewModel.SubmitAttempted = false;
                windowManager.ScrollFirstErrorIntoView(viewModel);
                
                return;
            }

            viewModel.InProgress = true;

            var instanceMetadata = new MonitoringNewInstance
            {
                DisplayName = viewModel.InstanceName,
                Name = viewModel.InstanceName.Replace(' ', '.'),
                ServiceDescription = viewModel.Description,
                InstallPath = viewModel.DestinationPath,
                LogPath = viewModel.LogPath,
                HostName = viewModel.HostName,
                Port = Convert.ToInt32(viewModel.PortNumber),
                ErrorQueue = viewModel.ErrorQueueName,
                TransportPackage = viewModel.SelectedTransport,
                ConnectionString = viewModel.ConnectionString,
                ServiceAccount = viewModel.ServiceAccount,
                ServiceAccountPwd = viewModel.Password
            };
            
            using (var progress = viewModel.GetProgressObject("ADDING INSTANCE"))
            {
                var reportCard = await Task.Run(() => installer.Add(instanceMetadata, progress, PromptToProceed));

                if (reportCard.HasErrors || reportCard.HasWarnings)
                {
                    windowManager.ShowActionReport(reportCard, "ISSUES ADDING INSTANCE", "Could not add new instance because of the following errors:", "There were some warnings while adding the instance:");
                    return;
                }

                if (reportCard.CancelRequested)
                {
                    return;
                }
            }

            viewModel.TryClose(true);

            eventAggregator.PublishOnUIThread(new RefreshInstances());
        }

        private bool PromptToProceed(PathInfo pathInfo)
        {
            var result = false;

            Execute.OnUIThread(() =>
            {
                result = windowManager.ShowYesNoDialog("ADDING INSTANCE QUESTION - DIRECTORY NOT EMPTY", $"The directory specified as the {pathInfo.Name} is not empty.", $"Are you sure you want to use '{pathInfo.Path}' ?", "Yes use it", "No I want to change it");
            });

            return result;
        }
    }
}