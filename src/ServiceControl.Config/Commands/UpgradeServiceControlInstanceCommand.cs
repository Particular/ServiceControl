namespace ServiceControl.Config.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.ServiceProcess;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using FluentValidation;
    using Framework;
    using Framework.Commands;
    using ServiceControl.Config.Events;
    using ServiceControl.Config.Framework.Modules;
    using ServiceControl.Config.UI.InstanceDetails;
    using ServiceControl.Config.UI.MessageBox;
    using ServiceControl.Config.Validation;
    using ServiceControl.Config.Xaml.Controls;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.ReportCard;

    class UpgradeServiceControlInstanceCommand : AwaitableAbstractCommand<InstanceDetailsViewModel>
    {
        private readonly IEventAggregator eventAggregator;
        private readonly ServiceControlInstanceInstaller installer;
        private readonly IWindowManagerEx windowManager;

        public UpgradeServiceControlInstanceCommand(Func<InstanceDetailsViewModel, bool> canExecuteMethod = null) : base(canExecuteMethod)
        {
        }

        public UpgradeServiceControlInstanceCommand(IWindowManagerEx windowManager, IEventAggregator eventAggregator, ServiceControlInstanceInstaller installer)
        {
            this.windowManager = windowManager;
            this.eventAggregator = eventAggregator;
            this.installer = installer;
        }

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

            var upgradeOptions = new ServiceControlUpgradeOptions();

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
                    var viewModel = new SliderDialogViewModel("UPGRADE QUESTION - DATABASE RETENTION",
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
                var viewModel = new SliderDialogViewModel("UPGRADE QUESTION - DATABASE RETENTION",
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
                var viewModel = new TextBoxDialogViewModel("UPGRADE QUESTION - MAINTENANCE PORT",
                    "When in the maintenance mode Service Control exposes the RavenDB database on a specified port.",
                    "MAINTENANCE PORT",
                    "", new MaintenancePortValidator());

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

            if (instance.Version.Major != installer.ZipInfo.Version.Major) //Upgrade to different major -> recommend DB backup
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

                var dbSize = instance.GetDatabaseSize();
                if (dbSize >= 100 * 1024 * 1024 * 1024) // 100GB
                {
                    if (!windowManager.ShowYesNoDialog($"MIGRATE LARGE DATABASE", $"The database being upgraded is {dbSize.ToString("N0")} Gb. Migrating this much data could take a long "
                        + "time and ServiceControl will be stopped for that entire duration. It is recommended that you consider one of the other upgrade approaches instead.",
                        "Are you sure you want to migrate this database?", "Yes", "No"))
                    {
                        return;
                    }
                }
            }
            else
            {
                if(instance.Service.Status != ServiceControllerStatus.Stopped &&
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

                reportCard = await Task.Run(() => installer.Upgrade(model.Name, upgradeOptions, progress));

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

        [FeatureToggle(Feature.LicenseChecks)]
        public bool LicenseChecks { get; set; }

        class MaintenancePortValidator : AbstractValidator<TextBoxDialogViewModel>
        {
            ReadOnlyCollection<ServiceControlInstance> ServiceControlInstances;

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
                    .SelectMany(p => new[] { p.Port.ToString(), p.DatabaseMaintenancePort.ToString() })
                    .Distinct()
                    .ToList();
            }
        }
    }
}
