namespace ServiceControl.Config.UI.InstanceAdd
{
    using System;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using Events;
    using Framework;
    using Framework.Modules;
    using ReactiveUI;
    using ServiceControl.Engine.Extensions;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Validation;
    using Validation;

    class ServiceControlAddAttachment : Attachment<ServiceControlAddViewModel>
    {
        public ServiceControlAddAttachment(IServiceControlWindowManager windowManager, IEventAggregator eventAggregator, ServiceControlInstanceInstaller serviceControlInstaller, ServiceControlAuditInstanceInstaller serviceControlAuditInstaller)
        {
            this.windowManager = windowManager;
            this.serviceControlInstaller = serviceControlInstaller;
            this.serviceControlAuditInstaller = serviceControlAuditInstaller;
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

        bool IsInProgress() => viewModel != null && !viewModel.InProgress;

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

            var serviceControlNewInstance = viewModel.InstallErrorInstance ? new ServiceControlNewInstance
            {
                DisplayName = viewModel.ServiceControl.InstanceName,
                Name = viewModel.ServiceControl.InstanceName.Replace(' ', '.'),
                ServiceDescription = viewModel.ServiceControl.Description,
                DBPath = viewModel.ServiceControl.DatabasePath,
                LogPath = viewModel.ServiceControl.LogPath,
                InstallPath = viewModel.ServiceControl.DestinationPath,
                HostName = viewModel.ServiceControl.HostName,
                Port = Convert.ToInt32(viewModel.ServiceControl.PortNumber),
                DatabaseMaintenancePort = Convert.ToInt32(viewModel.ServiceControl.DatabaseMaintenancePortNumber),
                VirtualDirectory = null,
                ErrorQueue = viewModel.ServiceControl.ErrorQueueName,
                ErrorLogQueue = viewModel.ServiceControl.ErrorForwarding.Value ? viewModel.ServiceControl.ErrorForwardingQueueName : null,
                ForwardErrorMessages = viewModel.ServiceControl.ErrorForwarding.Value,
                TransportPackage = viewModel.SelectedTransport,
                ConnectionString = viewModel.ConnectionString,
                ErrorRetentionPeriod = viewModel.ServiceControl.ErrorRetentionPeriod,
                ServiceAccount = viewModel.ServiceControl.ServiceAccount,
                ServiceAccountPwd = viewModel.ServiceControl.Password,
                EnableFullTextSearchOnBodies = viewModel.ServiceControl.EnableFullTextSearchOnBodies.Value
            } : null;

            var auditNewInstance = viewModel.InstallAuditInstance ? ServiceControlAuditNewInstance.CreateWithDefaultPersistence() : null;
            if (viewModel.InstallAuditInstance)
            {
                auditNewInstance.DisplayName = viewModel.ServiceControlAudit.InstanceName;
                auditNewInstance.Name = viewModel.ServiceControlAudit.InstanceName.Replace(' ', '.');
                auditNewInstance.ServiceDescription = viewModel.ServiceControlAudit.Description;
                auditNewInstance.DBPath = viewModel.ServiceControlAudit.DatabasePath;
                auditNewInstance.LogPath = viewModel.ServiceControlAudit.LogPath;
                auditNewInstance.InstallPath = viewModel.ServiceControlAudit.DestinationPath;
                auditNewInstance.HostName = viewModel.ServiceControlAudit.HostName;
                auditNewInstance.Port = Convert.ToInt32(viewModel.ServiceControlAudit.PortNumber);
                auditNewInstance.DatabaseMaintenancePort = Convert.ToInt32(viewModel.ServiceControlAudit.DatabaseMaintenancePortNumber);
                auditNewInstance.AuditLogQueue = viewModel.ServiceControlAudit.AuditForwarding.Value ? viewModel.ServiceControlAudit.AuditForwardingQueueName : null;
                auditNewInstance.AuditQueue = viewModel.ServiceControlAudit.AuditQueueName;
                auditNewInstance.ForwardAuditMessages = viewModel.ServiceControlAudit.AuditForwarding.Value;
                auditNewInstance.TransportPackage = viewModel.SelectedTransport;
                auditNewInstance.ConnectionString = viewModel.ConnectionString;
                auditNewInstance.AuditRetentionPeriod = viewModel.ServiceControlAudit.AuditRetentionPeriod;
                auditNewInstance.ServiceAccount = viewModel.ServiceControlAudit.ServiceAccount;
                auditNewInstance.ServiceAccountPwd = viewModel.ServiceControlAudit.Password;
                auditNewInstance.ServiceControlQueueAddress = serviceControlNewInstance == null ? string.Empty : serviceControlNewInstance.Name;
                auditNewInstance.EnableFullTextSearchOnBodies = viewModel.ServiceControlAudit.EnableFullTextSearchOnBodies.Value;
            }

            var transportPackage = serviceControlNewInstance != null ? serviceControlNewInstance.TransportPackage : auditNewInstance.TransportPackage;
            if (transportPackage.IsLatestRabbitMQTransport() &&
                !await windowManager.ShowYesNoDialog("INSTALL WARNING", $"ServiceControl version {serviceControlInstaller.ZipInfo.Version} requires RabbitMQ broker version 3.10.0 or higher. Also, the stream_queue and quorum_queue feature flags must be enabled on the broker. Please confirm your broker meets the minimum requirements before installing.",
                                                     "Do you want to proceed?",
                                                     "Yes, my RabbitMQ broker meets the minimum requirements",
                                                     "No, cancel the install"))
            {
                viewModel.InProgress = false;
                return;
            }

            if (viewModel.InstallAuditInstance && viewModel.InstallErrorInstance)
            {
                serviceControlNewInstance.AddRemoteInstance(auditNewInstance.Url);
            }

            if (viewModel.InstallErrorInstance)
            {
                using (var progress = viewModel.GetProgressObject("ADDING INSTANCE"))
                {
                    var installationCancelled = await InstallInstance(serviceControlNewInstance, progress);
                    if (installationCancelled)
                    {
                        return;
                    }
                }
            }

            if (viewModel.InstallAuditInstance)
            {
                using (var progress = viewModel.GetProgressObject("ADDING AUDIT INSTANCE"))
                {
                    var installationCancelled = await InstallInstance(auditNewInstance, progress);
                    if (installationCancelled)
                    {
                        return;
                    }
                }
            }

            await viewModel.TryCloseAsync(true);

            await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
        }

        async Task<bool> InstallInstance(ServiceControlNewInstance instanceData, IProgressObject progress)
        {
            var reportCard = await Task.Run(() => serviceControlInstaller.Add(instanceData, progress, PromptToProceed));

            if (reportCard.HasErrors || reportCard.HasWarnings)
            {
                await windowManager.ShowActionReport(reportCard, "ISSUES ADDING INSTANCE", "Could not add new instance because of the following errors:", "There were some warnings while adding the instance:");
                return true;
            }

            if (reportCard.CancelRequested)
            {
                return true;
            }

            return false;
        }

        async Task<bool> InstallInstance(ServiceControlAuditNewInstance instanceData, IProgressObject progress)
        {
            var reportCard = await Task.Run(() => serviceControlAuditInstaller.Add(instanceData, progress, PromptToProceed));

            if (reportCard.HasErrors || reportCard.HasWarnings)
            {
                await windowManager.ShowActionReport(reportCard, "ISSUES ADDING INSTANCE", "Could not add new instance because of the following errors:", "There were some warnings while adding the instance:");
                return true;
            }

            if (reportCard.CancelRequested)
            {
                return true;
            }

            return false;
        }

        async Task<bool> PromptToProceed(PathInfo pathInfo)
        {
            var result = false;

            await Execute.OnUIThreadAsync(async () => { result = await windowManager.ShowYesNoDialog("ADDING INSTANCE QUESTION - DIRECTORY NOT EMPTY", $"The directory specified as the {pathInfo.Name} is not empty.", $"Are you sure you want to use '{pathInfo.Path}' ?", "Yes use it", "No I want to change it"); });

            return result;
        }

        readonly IServiceControlWindowManager windowManager;
        readonly IEventAggregator eventAggregator;
        readonly ServiceControlInstanceInstaller serviceControlInstaller;
        readonly ServiceControlAuditInstanceInstaller serviceControlAuditInstaller;
    }
}