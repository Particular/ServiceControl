namespace ServiceControl.Config.UI.InstanceAdd
{
    using System;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using Events;
    using Framework;
    using Framework.Modules;
    using ReactiveUI;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Validation;
    using Validation;

    class MonitoringAddAttachment : Attachment<MonitoringAddViewModel>
    {
        public MonitoringAddAttachment(IServiceControlWindowManager windowManager, IEventAggregator eventAggregator, MonitoringInstanceInstaller installer)
        {
            this.windowManager = windowManager;
            this.installer = installer;
            this.eventAggregator = eventAggregator;
        }

        protected override void OnAttach()
        {
            var validationTemplate = new ValidationTemplate(viewModel);
            viewModel.ValidationTemplate = validationTemplate;

            viewModel.Save = ReactiveCommand.CreateFromTask(Add);
            viewModel.Cancel = Command.Create(async () =>
            {
                await viewModel.TryCloseAsync(false);
                await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
            }, IsInProgress);
        }

        bool IsInProgress()
        {
            return viewModel != null && !viewModel.InProgress;
        }

        async Task Add()
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

            if ((instanceMetadata.TransportPackage.Name == TransportNames.RabbitMQClassicConventionalRoutingTopology ||
                 instanceMetadata.TransportPackage.Name == TransportNames.RabbitMQQuorumConventionalRoutingTopology ||
                 instanceMetadata.TransportPackage.Name == TransportNames.RabbitMQClassicDirectRoutingTopology ||
                 instanceMetadata.TransportPackage.Name == TransportNames.RabbitMQQuorumDirectRoutingTopology) &&
                !await windowManager.ShowYesNoDialog("INSTALL WARNING", $"ServiceControl version {installer.ZipInfo.Version} requires RabbitMQ broker version 3.10.0 or higher. Also, the stream_queue and quorum_queue feature flags must be enabled on the broker. Please confirm your broker meets the minimum requirements before installing.",
                                                     "Do you want to proceed?",
                                                     "Yes, my RabbitMQ broker meets the minimum requirements",
                                                     "No, cancel the install"))
            {
                viewModel.InProgress = false;
                return;
            }

            using (var progress = viewModel.GetProgressObject("ADDING INSTANCE"))
            {
                var reportCard = await Task.Run(() => installer.Add(instanceMetadata, progress, PromptToProceed));

                if (reportCard.HasErrors || reportCard.HasWarnings)
                {
                    await windowManager.ShowActionReport(reportCard, "ISSUES ADDING INSTANCE", "Could not add new instance because of the following errors:", "There were some warnings while adding the instance:");
                    return;
                }

                if (reportCard.CancelRequested)
                {
                    return;
                }
            }

            await viewModel.TryCloseAsync(true);

            await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
        }

        async Task<bool> PromptToProceed(PathInfo pathInfo)
        {
            var result = false;

            await Execute.OnUIThreadAsync(async () => { result = await windowManager.ShowYesNoDialog("ADDING INSTANCE QUESTION - DIRECTORY NOT EMPTY", $"The directory specified as the {pathInfo.Name} is not empty.", $"Are you sure you want to use '{pathInfo.Path}' ?", "Yes use it", "No I want to change it"); });

            return result;
        }

        readonly IServiceControlWindowManager windowManager;
        readonly IEventAggregator eventAggregator;
        readonly MonitoringInstanceInstaller installer;
    }
}