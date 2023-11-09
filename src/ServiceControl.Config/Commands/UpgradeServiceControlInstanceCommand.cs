namespace ServiceControl.Config.Commands
{
    using System;
    using System.Diagnostics;
    using System.ServiceProcess;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using Events;
    using FluentValidation;
    using Framework;
    using Framework.Commands;
    using Framework.Modules;
    using ServiceControl.Engine.Extensions;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.ReportCard;
    using ServiceControlInstaller.Engine.Validation;
    using UI.InstanceDetails;
    using UI.MessageBox;
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
            ServiceControlInstanceInstaller serviceControlInstaller
            )
        {
            this.windowManager = windowManager;
            this.eventAggregator = eventAggregator;
            this.serviceControlInstaller = serviceControlInstaller;
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

            var compatibleStorageEngine = instance.PersistenceManifest.Name == StorageEngineNames.RavenDB;

            if (!compatibleStorageEngine)
            {
                var upgradeGuide4to5url = "https://docs.particular.net/servicecontrol/upgrades/4to5/";

                var openUpgradeGuide = await windowManager.ShowYesNoDialog("STORAGE ENGINE INCOMPATIBLE",
                    $"Please note that the storage format has changed and the {instance.PersistenceManifest.DisplayName} storage engine is no longer available. Upgrading requires a side-by-side deployment of both versions. Migration guidance is available in the version 4 to 5 upgrade guidance at {upgradeGuide4to5url}",
                    "Open online ServiceControl 4 to 5 upgrade guide in system default browser?",
                    "Yes",
                    "No"
                );

                if (openUpgradeGuide)
                {
                    Process.Start(new ProcessStartInfo(upgradeGuide4to5url) { UseShellExecute = true });
                }

                return;
            }

            if (UpgradeControl.HasIncompatibleVersion(instance.Version))
            {
                var upgradePath = UpgradeControl.GetUpgradePathFor(instance.Version);
                var upgradePathText = string.Join<Version>(" -> ", upgradePath);
                var nextVersion = upgradePath[0];
                await windowManager.ShowMessage("VERSION UPGRADE INCOMPATIBLE",
                    "<Section xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xml:space=\"preserve\" TextAlignment=\"Left\" LineHeight=\"Auto\" IsHyphenationEnabled=\"False\" xml:lang=\"en-us\">\r\n" +
                    $"<Paragraph>You must upgrade to version(s) {upgradePathText} before upgrading to version {serviceControlInstaller.ZipInfo.Version}:</Paragraph>\r\n" +
                    "<List MarkerStyle=\"Decimal\" Margin=\"0,0,0,0\" Padding=\"0,0,0,0\">\r\n" +
                    $"<ListItem Margin=\"48,0,0,0\"><Paragraph>Download and install version {nextVersion} from https://github.com/Particular/ServiceControl/releases/tag/{nextVersion}</Paragraph></ListItem>" +
                    $"<ListItem Margin=\"48,0,0,0\"><Paragraph>Upgrade this instance to version {nextVersion}.</Paragraph></ListItem>\r\n" +
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

            var upgradeOptions = new ServiceControlUpgradeOptions();

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

            if (DotnetVersionValidator.FrameworkRequirementsAreMissing(needsRavenDB: true, out var missingMessage))
            {
                await windowManager.ShowMessage("Missing prerequisites", missingMessage, acceptText: "Cancel", hideCancel: true);
                return;
            }

            if (instance.TransportPackage.IsOldRabbitMQTransport() &&
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

            await UpgradeServiceControlInstance(model, instance, upgradeOptions);

            await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
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

        readonly IEventAggregator eventAggregator;
        readonly IServiceControlWindowManager windowManager;
        readonly ServiceControlInstanceInstaller serviceControlInstaller;

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