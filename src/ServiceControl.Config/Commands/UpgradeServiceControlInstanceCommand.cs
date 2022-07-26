namespace ServiceControl.Config.Commands
{
    using System;
    using System.ServiceProcess;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using Events;
    using FluentValidation;
    using Framework;
    using Framework.Commands;
    using Framework.Modules;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.ReportCard;
    using ServiceControlInstaller.Engine.Validation;
    using UI.InstanceDetails;
    using UI.MessageBox;
    using UI.Upgrades;
    using Validation;
    using Xaml.Controls;
    using Validations = Extensions.Validations;

    class UpgradeServiceControlInstanceCommand : AwaitableAbstractCommand<InstanceDetailsViewModel>
    {
        public UpgradeServiceControlInstanceCommand(Func<InstanceDetailsViewModel, bool> canExecuteMethod = null) : base(canExecuteMethod)
        {
        }

        public UpgradeServiceControlInstanceCommand(
            IServiceControlWindowManager windowManager,
            IEventAggregator eventAggregator,
            ServiceControlInstanceInstaller serviceControlInstaller,
            ServiceControlAuditInstanceInstaller serviceControlAuditInstaller,
            Func<string, AddNewAuditInstanceViewModel> auditUpgradeViewModelFactory)
        {
            this.windowManager = windowManager;
            this.eventAggregator = eventAggregator;
            this.serviceControlInstaller = serviceControlInstaller;
            this.serviceControlAuditInstaller = serviceControlAuditInstaller;
            this.auditUpgradeViewModelFactory = auditUpgradeViewModelFactory;
        }

        [FeatureToggle(Feature.LicenseChecks)]
        public bool LicenseChecks { get; set; }

        public override async Task ExecuteAsync(InstanceDetailsViewModel model)
        {
            if (LicenseChecks)
            {
                var licenseCheckResult = serviceControlInstaller.CheckLicenseIsValid();
                if (!licenseCheckResult.Valid)
                {
                    await windowManager.ShowMessage("LICENSE ERROR", $"Upgrade could not continue due to an issue with the current license. {licenseCheckResult.Message}.  Contact contact@particular.net", hideCancel: true);
                    return;
                }
            }

            var instance = InstanceFinder.FindInstanceByName<ServiceControlInstance>(model.Name);

            instance.Service.Refresh();

            var upgradeInfo = UpgradeControl.GetUpgradeInfoForTargetVersion(serviceControlInstaller.ZipInfo.Version, instance.Version);
            var upgradeOptions = new ServiceControlUpgradeOptions { UpgradeInfo = upgradeInfo };

            var upgradeAction = instance.GetRequiredUpgradeAction(serviceControlInstaller.ZipInfo.Version);
            var shouldInstallAudit = upgradeAction == RequiredUpgradeAction.SplitOutAudit;

            AddNewAuditInstanceViewModel auditViewModel = null;

            if (instance.Version < upgradeInfo.CurrentMinimumVersion)
            {
                await windowManager.ShowMessage("VERSION UPGRADE INCOMPATIBLE",
                    "<Section xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xml:space=\"preserve\" TextAlignment=\"Left\" LineHeight=\"Auto\" IsHyphenationEnabled=\"False\" xml:lang=\"en-us\">\r\n" +
                    $"<Paragraph>You must upgrade to version {upgradeInfo.RecommendedUpgradeVersion} before upgrading to version {serviceControlInstaller.ZipInfo.Version}:</Paragraph>\r\n" +
                    "<List MarkerStyle=\"Decimal\" Margin=\"0,0,0,0\" Padding=\"0,0,0,0\">\r\n" +
                    $"<ListItem Margin=\"48,0,0,0\"><Paragraph>Uninstall version {serviceControlInstaller.ZipInfo.Version}.</Paragraph></ListItem>\r\n" +
                    $"<ListItem Margin=\"48,0,0,0\"><Paragraph>Download and install version {upgradeInfo.RecommendedUpgradeVersion} from https://github.com/Particular/ServiceControl/releases/tag/{upgradeInfo.RecommendedUpgradeVersion}</Paragraph></ListItem>" +
                    $"<ListItem Margin=\"48,0,0,0\"><Paragraph>Upgrade this instance to version {upgradeInfo.RecommendedUpgradeVersion}.</Paragraph></ListItem>\r\n" +
                    "<ListItem Margin=\"48,0,0,0\"><Paragraph>Download and install the latest version from https://particular.net/start-servicecontrol-download</Paragraph></ListItem>\r\n" +
                    "<ListItem Margin=\"48,0,0,0\"><Paragraph>Upgrade this instance to the latest version of ServiceControl.</Paragraph></ListItem>\r\n" +
                    "</List>\r\n" +
                    "</Section>",
                    hideCancel: true);

                return;
            }

            if (instance.IsErrorQueueDisabled())
            {
                await windowManager.ShowMessage("UPGRADE INCOMPATIBLE",
                    "You cannot upgrade the instance of ServiceControl with error ingestion disabled. Please contact support.",
                    hideCancel: true);

                return;
            }

            if (!instance.AppConfig.AppSettingExists(ServiceControlSettings.ForwardErrorMessages.Name))
            {
                var result = await windowManager.ShowYesNoCancelDialog("UPGRADE QUESTION - DISABLE ERROR FORWARDING", "Error messages can be forwarded to a secondary error queue known as the Error Forwarding Queue. This queue exists to allow external tools to receive error messages. If you do not have a tool processing messages from the Error Forwarding Queue this setting should be disabled.", "So what do you want to do ?", "Do NOT forward", "Yes I want to forward");
                if (!result.HasValue)
                {
                    //Dialog was cancelled
                    await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
                    return;
                }

                upgradeOptions.OverrideEnableErrorForwarding = !result.Value;
            }

            //Grab old setting if it exists
            if (!instance.AppConfig.AppSettingExists(ServiceControlSettings.AuditRetentionPeriod.Name))
            {
                if (instance.AppConfig.AppSettingExists(ServiceControlSettings.HoursToKeepMessagesBeforeExpiring.Name))
                {
                    var i = instance.AppConfig.Read(ServiceControlSettings.HoursToKeepMessagesBeforeExpiring.Name, -1);
                    if (i != -1)
                    {
                        upgradeOptions.AuditRetentionPeriod = TimeSpan.FromHours(i);
                    }
                }
            }

            if (!instance.AppConfig.AppSettingExists(ServiceControlSettings.ErrorRetentionPeriod.Name))
            {
                var viewModel = new SliderDialogViewModel("INPUT REQUIRED - DATABASE RETENTION",
                    "Service Control periodically purges resolved and archived error messages from the database.",
                    "ERROR RETENTION PERIOD",
                    "Please specify the age at which these records should be removed",
                    TimeSpanUnits.Days,
                    SettingConstants.ErrorRetentionPeriodMinInDays,
                    SettingConstants.ErrorRetentionPeriodMaxInDays,
                    1,
                    1,
                    SettingConstants.ErrorRetentionPeriodDefaultInDaysForUI);

                if (await windowManager.ShowSliderDialog(viewModel))
                {
                    upgradeOptions.ErrorRetentionPeriod = viewModel.Period;
                }
                else
                {
                    //Dialog was cancelled
                    await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
                    return;
                }
            }

            if (!instance.AppConfig.AppSettingExists(ServiceControlSettings.DatabaseMaintenancePort.Name))
            {
                var viewModel = new TextBoxDialogViewModel("INPUT REQUIRED - MAINTENANCE PORT",
                    "When Service Control is set to maintenance mode it requires a prereserved port on which it exposes the RavenDB database.",
                    "MAINTENANCE PORT",
                    "Please specify an open port that will be used as the maintenance port",
                    new PortValidator());

                if (await windowManager.ShowTextBoxDialog(viewModel))
                {
                    upgradeOptions.MaintenancePort = int.Parse(viewModel.Value);
                }
                else
                {
                    //Dialog was cancelled
                    await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
                    return;
                }
            }

            if (!instance.AppConfig.AppSettingExists(ServiceControlSettings.EnableFullTextSearchOnBodies.Name))
            {
                var dialogResult = await windowManager.ShowYesNoCancelDialog(
                    "INPUT REQUIRED - FULL TEXT SEARCH ON MESSAGE BODIES",
                    "ServiceControl indexes message bodies to enable searching for messages by their contents in ServiceInsight. This has a performance impact on the ServiceControl instance and the feature can be disabled if it is not required.", "Do you want to disable full text search for message bodies?", "YES", "NO");
                if (dialogResult.HasValue)
                {
                    if (dialogResult.Value)
                    {
                        upgradeOptions.DisableFullTextSearchOnBodies = true;
                    }
                }
                else
                {
                    //Dialog was cancelled
                    await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
                    return;
                }
            }

            if (shouldInstallAudit)
            {
                auditViewModel = auditUpgradeViewModelFactory(instance.Name);
                auditViewModel.ServiceControlAudit.SetupServiceAccount(instance);
                auditViewModel.ServiceControlAudit.SetFullTextSearchOnBodies(
                    instance.EnableFullTextSearchOnBodies
                    && !upgradeOptions.DisableFullTextSearchOnBodies);

                if (await windowManager.ShowInnerDialog(auditViewModel) != true)
                {
                    //Dialog was cancelled
                    await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
                    return;
                }
            }

            if ((instance.TransportPackage.Name == TransportNames.RabbitMQConventionalRoutingTopologyDeprecated ||
                 instance.TransportPackage.Name == TransportNames.RabbitMQDirectRoutingTopologyDeprecated) &&
                 !await windowManager.ShowYesNoDialog("UPGRADE WARNING", $"ServiceControl version {serviceControlInstaller.ZipInfo.Version} requires RabbitMQ broker version 3.10.0 or higher. Also, the stream_queue and quorum_queue feature flags must be enabled on the broker. Please confirm your broker meets the minimum requirements before upgrading.",
                   "Do you want to proceed?",
                   "Yes, my RabbitMQ broker meets the minimum requirements",
                   "No, cancel the upgrade"))
            {
                return;
            }

            if (instance.Service.Status != ServiceControllerStatus.Stopped &&
                !await windowManager.ShowYesNoDialog($"STOP INSTANCE AND UPGRADE TO {serviceControlInstaller.ZipInfo.Version}",
                    $"{model.Name} needs to be stopped in order to upgrade to version {serviceControlInstaller.ZipInfo.Version}.",
                    "Do you want to proceed?",
                    "Yes, I want to proceed", "No"))
            {
                return;
            }

            if (shouldInstallAudit)
            {
                var auditInstalled = await InstallServiceControlAudit(model, auditViewModel, instance);
                if (!auditInstalled)
                {
                    //Dialog was cancelled
                    await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
                    return;
                }

                var auditInstance = InstanceFinder.FindInstanceByName<ServiceControlAuditInstance>(auditViewModel.ServiceControlAudit.InstanceName);
                upgradeOptions.RemoteUrl = auditInstance.BrowsableUrl;
            }

            await UpgradeServiceControlInstance(model, instance, upgradeOptions);

            await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
        }

        async Task<bool> InstallServiceControlAudit(InstanceDetailsViewModel detailsViewModel, AddNewAuditInstanceViewModel viewModel, ServiceControlInstance instance)
        {
            var auditNewInstance = new ServiceControlAuditNewInstance
            {
                //Read from user configured values
                DisplayName = viewModel.ServiceControlAudit.InstanceName,
                Name = viewModel.ServiceControlAudit.InstanceName.Replace(' ', '.'),
                ServiceDescription = viewModel.ServiceControlAudit.Description,
                DBPath = viewModel.ServiceControlAudit.DatabasePath,
                LogPath = viewModel.ServiceControlAudit.LogPath,
                InstallPath = viewModel.ServiceControlAudit.DestinationPath,
                HostName = viewModel.ServiceControlAudit.HostName,
                Port = Convert.ToInt32(viewModel.ServiceControlAudit.PortNumber),
                DatabaseMaintenancePort = Convert.ToInt32(viewModel.ServiceControlAudit.DatabaseMaintenancePortNumber),
                ServiceAccount = viewModel.ServiceControlAudit.ServiceAccount,
                ServiceAccountPwd = viewModel.ServiceControlAudit.Password,
                EnableFullTextSearchOnBodies = viewModel.ServiceControlAudit.EnableFullTextSearchOnBodies.Value,

                //Copy from existing ServiceControl instance
                AuditLogQueue = instance.AuditLogQueue,
                AuditQueue = instance.AuditQueue,
                ForwardAuditMessages = instance.ForwardAuditMessages,
                AuditRetentionPeriod = instance.AuditRetentionPeriod.Value,
                TransportPackage = instance.TransportPackage,
                ConnectionString = instance.ConnectionString,
                ServiceControlQueueAddress = instance.Name
            };

            using (var progress = detailsViewModel.GetProgressObject("ADDING AUDIT INSTANCE"))
            {
                var installationCancelled = await InstallInstance(auditNewInstance, progress);
                if (installationCancelled)
                {
                    return false;
                }
            }

            return true;
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

        async Task UpgradeServiceControlInstance(InstanceDetailsViewModel model, ServiceControlInstance instance, ServiceControlUpgradeOptions upgradeOptions)
        {
            using (var progress = model.GetProgressObject($"UPGRADING {model.Name}"))
            {
                var reportCard = new ReportCard();
                var restartAgain = model.IsRunning;

                var stopped = await model.StopService(progress);

                if (!stopped)
                {
                    await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());

                    reportCard.Errors.Add("Failed to stop the service");
                    reportCard.SetStatus();
                    await windowManager.ShowActionReport(reportCard, "ISSUES UPGRADING INSTANCE", "Could not upgrade instance because of the following errors:");

                    return;
                }

                reportCard = await Task.Run(() => serviceControlInstaller.Upgrade(instance, upgradeOptions, progress));

                if (reportCard.HasErrors || reportCard.HasWarnings)
                {
                    await windowManager.ShowActionReport(reportCard, "ISSUES UPGRADING INSTANCE", "Could not upgrade instance because of the following errors:", "There were some warnings while upgrading the instance:");
                }
                else
                {
                    if (restartAgain)
                    {
                        var serviceStarted = await model.StartService(progress);
                        if (!serviceStarted)
                        {
                            reportCard.Errors.Add("The Service failed to start. Please consult the ServiceControl logs for this instance");
                            await windowManager.ShowActionReport(reportCard, "UPGRADE FAILURE", "Instance reported this error after upgrade:");
                        }
                    }
                }
            }
        }

        async Task<bool> PromptToProceed(PathInfo pathInfo)
        {
            var result = false;

            await Execute.OnUIThreadAsync(async () => { result = await windowManager.ShowYesNoDialog("ADDING INSTANCE QUESTION - DIRECTORY NOT EMPTY", $"The directory specified as the {pathInfo.Name} is not empty.", $"Are you sure you want to use '{pathInfo.Path}' ?", "Yes use it", "No I want to change it"); });

            return result;
        }


        readonly IEventAggregator eventAggregator;
        readonly IServiceControlWindowManager windowManager;
        readonly ServiceControlInstanceInstaller serviceControlInstaller;
        readonly ServiceControlAuditInstanceInstaller serviceControlAuditInstaller;
        readonly Func<string, AddNewAuditInstanceViewModel> auditUpgradeViewModelFactory;

        class PortValidator : AbstractValidator<TextBoxDialogViewModel>
        {
            public PortValidator()
            {
                RuleFor(x => x.Value)
                    .NotEmpty()
                    .ValidPort()
                    .MustNotBeIn(x => Validations.UsedPorts())
                    .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Ports"));
            }
        }
    }
}