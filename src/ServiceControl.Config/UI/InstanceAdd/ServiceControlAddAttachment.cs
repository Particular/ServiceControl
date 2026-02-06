namespace ServiceControl.Config.UI.InstanceAdd
{
    using System;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using Events;
    using Framework;
    using Framework.Modules;
    using ReactiveUI;
    using ServiceControl.Config.Commands;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Validation;
    using Validation;

    class ServiceControlAddAttachment : Attachment<ServiceControlAddViewModel>
    {
        public ServiceControlAddAttachment(IServiceControlWindowManager windowManager, IEventAggregator eventAggregator, ServiceControlInstanceInstaller serviceControlInstaller, ServiceControlAuditInstanceInstaller serviceControlAuditInstaller, ScmuCommandChecks commandChecks)
        {
            this.windowManager = windowManager;
            this.serviceControlInstaller = serviceControlInstaller;
            this.serviceControlAuditInstaller = serviceControlAuditInstaller;
            this.eventAggregator = eventAggregator;
            this.commandChecks = commandChecks;
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

            var serviceControlNewInstance = viewModel.InstallErrorInstance ? ServiceControlNewInstance.CreateWithDefaultPersistence() : null;

            if (viewModel.InstallErrorInstance)
            {
                serviceControlNewInstance.DisplayName = viewModel.ServiceControl.InstanceName;
                serviceControlNewInstance.Name = viewModel.ServiceControl.InstanceName.Replace(' ', '.');
                serviceControlNewInstance.InstanceName = viewModel.ServiceControl.InstanceName.Replace(' ', '.');
                serviceControlNewInstance.ServiceDescription = viewModel.ServiceControl.Description;
                serviceControlNewInstance.DBPath = viewModel.ServiceControl.DatabasePath;
                serviceControlNewInstance.LogPath = viewModel.ServiceControl.LogPath;
                serviceControlNewInstance.InstallPath = viewModel.ServiceControl.DestinationPath;
                serviceControlNewInstance.HostName = viewModel.ServiceControl.HostName;
                serviceControlNewInstance.Port = Convert.ToInt32(viewModel.ServiceControl.PortNumber);
                serviceControlNewInstance.DatabaseMaintenancePort = Convert.ToInt32(viewModel.ServiceControl.DatabaseMaintenancePortNumber);
                serviceControlNewInstance.VirtualDirectory = null;
                serviceControlNewInstance.ErrorQueue = viewModel.ServiceControl.ErrorQueueName;
                serviceControlNewInstance.ErrorLogQueue = viewModel.ServiceControl.ErrorForwarding.Value ? viewModel.ServiceControl.ErrorForwardingQueueName : null;
                serviceControlNewInstance.ForwardErrorMessages = viewModel.ServiceControl.ErrorForwarding.Value;
                serviceControlNewInstance.TransportPackage = viewModel.SelectedTransport;
                serviceControlNewInstance.ConnectionString = viewModel.ConnectionString;
                serviceControlNewInstance.ErrorRetentionPeriod = viewModel.ServiceControl.ErrorRetentionPeriod;
                serviceControlNewInstance.ServiceAccount = viewModel.ServiceControl.ServiceAccount;
                serviceControlNewInstance.ServiceAccountPwd = viewModel.ServiceControl.Password;
                serviceControlNewInstance.EnableFullTextSearchOnBodies = viewModel.ServiceControl.EnableFullTextSearchOnBodies.Value;
                serviceControlNewInstance.EnableIntegratedServicePulse = viewModel.ServiceControl.EnableIntegratedServicePulse.Value;
            }

            var auditNewInstance = viewModel.InstallAuditInstance ? ServiceControlAuditNewInstance.CreateWithDefaultPersistence() : null;
            if (viewModel.InstallAuditInstance)
            {
                auditNewInstance.DisplayName = viewModel.ServiceControlAudit.InstanceName;
                auditNewInstance.Name = viewModel.ServiceControlAudit.InstanceName.Replace(' ', '.');
                auditNewInstance.InstanceName = viewModel.ServiceControlAudit.InstanceName.Replace(' ', '.');
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
                auditNewInstance.ServiceControlQueueAddress = serviceControlNewInstance == null ? string.Empty : serviceControlNewInstance.InstanceName;
                auditNewInstance.EnableFullTextSearchOnBodies = viewModel.ServiceControlAudit.EnableFullTextSearchOnBodies.Value;
            }

            if (!await commandChecks.ValidateNewInstance(serviceControlNewInstance, auditNewInstance))
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
        readonly ScmuCommandChecks commandChecks;
    }
}