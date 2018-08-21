namespace ServiceControl.Config.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
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
    using UI.InstanceDetails;
    using UI.MessageBox;
    using Validation;
    using Xaml.Controls;

    class UpgradeServiceControlInstanceCommand : AwaitableAbstractCommand<InstanceDetailsViewModel>
    {
        public UpgradeServiceControlInstanceCommand(Func<InstanceDetailsViewModel, bool> canExecuteMethod = null) : base(canExecuteMethod)
        {
        }

        public UpgradeServiceControlInstanceCommand(IWindowManagerEx windowManager, IEventAggregator eventAggregator, ServiceControlInstanceInstaller installer)
        {
            this.windowManager = windowManager;
            this.eventAggregator = eventAggregator;
            this.installer = installer;
        }

        [FeatureToggle(Feature.LicenseChecks)]
        public bool LicenseChecks { get; set; }

        public override async Task ExecuteAsync(InstanceDetailsViewModel model)
        {
            if (LicenseChecks)
            {
                var licenseCheckResult = installer.CheckLicenseIsValid();
                if (!licenseCheckResult.Valid)
                {
                    windowManager.ShowMessage("LICENSE ERROR", $"Upgrade could not continue due to an issue with the current license. {licenseCheckResult.Message}.  Contact contact@particular.net", hideCancel: true);
                    return;
                }
            }

            var instance = InstanceFinder.FindServiceControlInstance(model.Name);

            instance.Service.Refresh();

            var upgradeInfo = UpgradeControl.GetUpgradeInfoForTargetVersion(installer.ZipInfo.Version, instance.Version);

            var upgradeOptions = new ServiceControlUpgradeOptions {UpgradeInfo = upgradeInfo};

            if (instance.Version < upgradeInfo.CurrentMinimumVersion)
            {
                windowManager.ShowMessage("VERSION UPGRADE INCOMPATIBLE",
                    "<Section xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xml:space=\"preserve\" TextAlignment=\"Left\" LineHeight=\"Auto\" IsHyphenationEnabled=\"False\" xml:lang=\"en-us\">\r\n" +
                    $"<Paragraph>You must upgrade to version {upgradeInfo.RecommendedUpgradeVersion} before upgrading to version {installer.ZipInfo.Version}:</Paragraph>\r\n" +
                    "<List MarkerStyle=\"Decimal\" Margin=\"0,0,0,0\" Padding=\"0,0,0,0\">\r\n" +
                    $"<ListItem Margin=\"48,0,0,0\"><Paragraph>Uninstall version {installer.ZipInfo.Version}.</Paragraph></ListItem>\r\n" +
                    $"<ListItem Margin=\"48,0,0,0\"><Paragraph>Download and install version {upgradeInfo.RecommendedUpgradeVersion} from https://github.com/Particular/ServiceControl/releases/tag/{upgradeInfo.RecommendedUpgradeVersion}</Paragraph></ListItem>" +
                    $"<ListItem Margin=\"48,0,0,0\"><Paragraph>Upgrade this instance to version {upgradeInfo.RecommendedUpgradeVersion}.</Paragraph></ListItem>\r\n" +
                    "<ListItem Margin=\"48,0,0,0\"><Paragraph>Download and install the latest version from https://particular.net/start-servicecontrol-download</Paragraph></ListItem>\r\n" +
                    "<ListItem Margin=\"48,0,0,0\"><Paragraph>Upgrade this instance to the latest version of ServiceControl.</Paragraph></ListItem>\r\n" +
                    "</List>\r\n" +
                    "</Section>",
                    hideCancel: true);

                return;
            }

            if (!instance.AppConfig.AppSettingExists(SettingsList.ForwardErrorMessages.Name))
            {
                var result = windowManager.ShowYesNoCancelDialog("UPGRADE QUESTION - DISABLE ERROR FORWARDING", "Error messages can be forwarded to a secondary error queue known as the Error Forwarding Queue. This queue exists to allow external tools to receive error messages. If you do not have a tool processing messages from the Error Forwarding Queue this setting should be disabled.", "So what do you want to do ?", "Do NOT forward", "Yes I want to forward");
                if (!result.HasValue)
                {
                    //Dialog was cancelled
                    eventAggregator.PublishOnUIThread(new RefreshInstances());
                    return;
                }

                upgradeOptions.OverrideEnableErrorForwarding = !result.Value;
            }

            //Grab old setting if it exists
            if (!instance.AppConfig.AppSettingExists(SettingsList.AuditRetentionPeriod.Name))
            {
                if (instance.AppConfig.AppSettingExists(SettingsList.HoursToKeepMessagesBeforeExpiring.Name))
                {
                    var i = instance.AppConfig.Read(SettingsList.HoursToKeepMessagesBeforeExpiring.Name, -1);
                    if (i != -1)
                    {
                        upgradeOptions.AuditRetentionPeriod = TimeSpan.FromHours(i);
                    }
                }

                // No setting to migrate so display dialog
                if (!upgradeOptions.AuditRetentionPeriod.HasValue)
                {
                    var viewModel = new SliderDialogViewModel("INPUT REQUIRED - DATABASE RETENTION",
                        "Service Control periodically purges audit messages from the database.",
                        "AUDIT RETENTION PERIOD",
                        "Please specify the age at which these records should be removed",
                        TimeSpanUnits.Hours,
                        SettingConstants.AuditRetentionPeriodMinInHours,
                        SettingConstants.AuditRetentionPeriodMaxInHours,
                        1,
                        24,
                        SettingConstants.AuditRetentionPeriodDefaultInHoursForUI);

                    if (windowManager.ShowSliderDialog(viewModel))
                    {
                        upgradeOptions.AuditRetentionPeriod = viewModel.Period;
                    }
                    else
                    {
                        //Dialog was cancelled
                        eventAggregator.PublishOnUIThread(new RefreshInstances());
                        return;
                    }
                }
            }

            if (!instance.AppConfig.AppSettingExists(SettingsList.ErrorRetentionPeriod.Name))
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

                if (windowManager.ShowSliderDialog(viewModel))
                {
                    upgradeOptions.ErrorRetentionPeriod = viewModel.Period;
                }
                else
                {
                    //Dialog was cancelled
                    eventAggregator.PublishOnUIThread(new RefreshInstances());
                    return;
                }
            }

            if (!instance.AppConfig.AppSettingExists(SettingsList.DatabaseMaintenancePort.Name))
            {
                var viewModel = new TextBoxDialogViewModel("INPUT REQUIRED - MAINTENANCE PORT",
                    "When Service Control is set to maintenance mode it requires a prereserved port on which it exposes the RavenDB database.",
                    "MAINTENANCE PORT",
                    "Please specify an open port that will be used as the maintenance port",
                    new MaintenancePortValidator());

                if (windowManager.ShowTextBoxDialog(viewModel))
                {
                    upgradeOptions.MaintenancePort = int.Parse(viewModel.Value);
                }
                else
                {
                    //Dialog was cancelled
                    eventAggregator.PublishOnUIThread(new RefreshInstances());
                    return;
                }
            }

            if (upgradeInfo.DataBaseUpdate) //Database is being updated -> recommend DB backup
            {
                if (!windowManager.ShowYesNoDialog($"STOP INSTANCE AND UPGRADE TO {installer.ZipInfo.Version}",
                    $"{model.Name} is going to be upgraded to version {installer.ZipInfo.Version} which uses a different storage format. Database migration will be conducted "
                    + " as part of the upgrade. It is recommended that you back up the database before upgrading. To read more about the back up process "
                    + " see https://docs.particular.net/servicecontrol/backup-sc-database.",
                    "Do you want to proceed?",
                    "Yes I backed up the database and I want to proceed", "No"))
                {
                    return;
                }

                var dbSize = instance.GetDatabaseSizeInGb();
                if (dbSize >= 100) // 100GB
                {
                    if (!windowManager.ShowYesNoDialog("MIGRATE LARGE DATABASE", $"The database being upgraded is {dbSize.ToString("N0")} GB. Migrating this much data could take a long "
                                                                                 + "time and ServiceControl will be stopped for that entire duration. It is recommended that you consider one of the other upgrade approaches instead.",
                        "Are you sure you want to migrate this database?", "Yes", "No"))
                    {
                        return;
                    }
                }
            }
            else
            {
                if (instance.Service.Status != ServiceControllerStatus.Stopped &&
                    !windowManager.ShowYesNoDialog($"STOP INSTANCE AND UPGRADE TO {installer.ZipInfo.Version}",
                        $"{model.Name} needs to be stopped in order to upgrade to version {installer.ZipInfo.Version}.",
                        "Do you want to proceed?",
                        "Yes I want to proceed", "No"))
                {
                    return;
                }
            }

            using (var progress = model.GetProgressObject($"UPGRADING {model.Name}"))
            {
                var reportCard = new ReportCard();
                var restartAgain = model.IsRunning;

                var stopped = await model.StopService(progress);

                if (!stopped)
                {
                    eventAggregator.PublishOnUIThread(new RefreshInstances());

                    reportCard.Errors.Add("Failed to stop the service");
                    reportCard.SetStatus();
                    windowManager.ShowActionReport(reportCard, "ISSUES UPGRADING INSTANCE", "Could not upgrade instance because of the following errors:");

                    return;
                }

                reportCard = await Task.Run(() => installer.Upgrade(instance, upgradeOptions, progress));

                if (reportCard.HasErrors || reportCard.HasWarnings)
                {
                    windowManager.ShowActionReport(reportCard, "ISSUES UPGRADING INSTANCE", "Could not upgrade instance because of the following errors:", "There were some warnings while upgrading the instance:");
                }
                else
                {
                    if (restartAgain)
                    {
                        var serviceStarted = await model.StartService(progress);
                        if (!serviceStarted)
                        {
                            reportCard.Errors.Add("The Service failed to start. Please consult the service control logs for this instance");
                            windowManager.ShowActionReport(reportCard, "UPGRADE FAILURE", "Instance reported this error after upgrade:");
                        }
                    }
                }
            }

            eventAggregator.PublishOnUIThread(new RefreshInstances());
        }

        readonly IEventAggregator eventAggregator;
        readonly ServiceControlInstanceInstaller installer;
        readonly IWindowManagerEx windowManager;

        class MaintenancePortValidator : AbstractValidator<TextBoxDialogViewModel>
        {
            public MaintenancePortValidator()
            {
                ServiceControlInstances = InstanceFinder.ServiceControlInstances();

                RuleFor(x => x.Value)
                    .NotEmpty()
                    .ValidPort()
                    .MustNotBeIn(x => UsedPorts())
                    .WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Ports");
            }

            List<string> UsedPorts()
            {
                return ServiceControlInstances
                    .SelectMany(p => new[]
                    {
                        p.Port.ToString(),
                        p.DatabaseMaintenancePort.ToString()
                    })
                    .Distinct()
                    .ToList();
            }

            ReadOnlyCollection<ServiceControlInstance> ServiceControlInstances;
        }
    }
}