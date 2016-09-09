namespace ServiceControl.Config.Commands
{
    using System;
    using System.ServiceProcess;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using Framework;
    using Framework.Commands;
    using ServiceControl.Config.Events;
    using ServiceControl.Config.Framework.Modules;
    using ServiceControl.Config.UI.InstanceDetails;
    using ServiceControl.Config.UI.MessageBox;
    using ServiceControl.Config.Xaml.Controls;
    using ServiceControlInstaller.Engine.Configuration;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.ReportCard;

    class UpgradeInstanceCommand : AwaitableAbstractCommand<InstanceDetailsViewModel>
    {
        private readonly IEventAggregator eventAggregator;
        private readonly Installer installer;
        private readonly IWindowManagerEx windowManager;

        public UpgradeInstanceCommand(Func<InstanceDetailsViewModel, bool> canExecuteMethod = null) : base(canExecuteMethod)
        {
        }

        public UpgradeInstanceCommand(IWindowManagerEx windowManager, IEventAggregator eventAggregator, Installer installer)
        {
            this.windowManager = windowManager;

            this.eventAggregator = eventAggregator;
            this.installer = installer;
        }

        public override async Task ExecuteAsync(InstanceDetailsViewModel model)
        {
            var instance = ServiceControlInstance.FindByName(model.Name);
            instance.Service.Refresh();

            var upgradeOptions = new InstanceUpgradeOptions();
            
            if (!instance.AppSettingExists(SettingsList.ForwardErrorMessages.Name))
            {
                var result  = windowManager.ShowYesNoCancelDialog("UPGRADE QUESTION - DISABLE ERROR FORWARDING", "Error messages can be forwarded to a secondary error queue known as the Error Forwarding Queue. This queue exists to allow external tools to receive error messages. If you do not have a tool processing messages from the Error Forwarding Queue this setting should be disabled.", "So what do you want to do ?", "Do NOT forward", "Yes I want to forward");
                if (!result.HasValue)
                {
                    //Dialog was cancelled
                    eventAggregator.PublishOnUIThread(new RefreshInstances());
                    return;
                }
                upgradeOptions.OverrideEnableErrorForwarding = !result.Value;
            }
            
            //Grab old setting if it exists
            if (!instance.AppSettingExists(SettingsList.AuditRetentionPeriod.Name))
            {
                if (instance.AppSettingExists(SettingsList.HoursToKeepMessagesBeforeExpiring.Name))
                {
                    var i = instance.ReadAppSetting(SettingsList.HoursToKeepMessagesBeforeExpiring.Name, -1);
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

            if (!instance.AppSettingExists(SettingsList.ErrorRetentionPeriod.Name))
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

            if (!instance.AppSettingExists(SettingsList.BodyStoragePath.Name))
            {
                var bodyStoragePathDialog = new FolderPickerDialogViewModel
                {
                    Title = "UPGRADE QUESTION - MESSAGE BODY STORAGE PATH",
                    Message = "Message bodies are now stored directly on disk rather than the database. For messages processed prior to upgrading the data will still reside within the database. You may choose to override the default location as part of the upgrade.</Paragraph><Paragraph>Note: The chosen path must be an empty local directory that is accessible by the current service account.",
                    Question = "Click OK to accept the the location for body storage",
                    PathHeader = "MESSAGE BODY STORAGE PATH",
                    YesButtonText = "OK",
                    HideNoButton = true,
                    ValidateFolderIsEmpty = true,
                    Path = instance.BodyStoragePath
                };

                var ingestionCacheDialogResult = windowManager.ShowOverlayDialog(bodyStoragePathDialog);
                if (ingestionCacheDialogResult == null)
                {
                    //Dialog was cancelled
                    eventAggregator.PublishOnUIThread(new RefreshInstances());
                    return;
                }
                if (ingestionCacheDialogResult.Value)
                {
                    upgradeOptions.BodyStoragePath = bodyStoragePathDialog.Path;
                }
            }
            
            if (!instance.AppSettingExists(SettingsList.IngestionCachePath.Name))
            {
                var ingestionCacheDialog = new FolderPickerDialogViewModel
                {
                    Title = "UPGRADE QUESTION - INGESTION CACHE PATH",
                    Message = "The ingestion cache is a temporary location for data prior to being processed. You may choose to override the default location as part of the upgrade.</Paragraph><Paragraph>Note: The chosen path must be an empty local directory that is accessible by the current service account.",
                    Question = "Click OK to accept the the location for ingestion cache",
                    PathHeader = "INGESTION CACHE LOCATION",
                    YesButtonText = "OK",
                    HideNoButton = true,
                    ValidateFolderIsEmpty = true,
                    Path = instance.IngestionCachePath
                };

                var ingestionCacheDialogResult = windowManager.ShowOverlayDialog(ingestionCacheDialog);
                if (ingestionCacheDialogResult == null)
                {
                    //Dialog was cancelled
                    eventAggregator.PublishOnUIThread(new RefreshInstances());
                    return;
                }
                if (ingestionCacheDialogResult.Value)
                {
                    upgradeOptions.IngestionCachePath = ingestionCacheDialog.Path;
                }
            }
            
            var backupDialog = new FolderPickerDialogViewModel
            {
                Title= "UPGRADE QUESTION - OPTIONAL DATABASE BACKUP",
                Message = "Instruct the service to backup up the database to a local path as part of the upgrade.</Paragraph><Paragraph>Note: The chosen path must be an empty local directory that is accessible by the current service account.",
                Question = "Backup the ServiceControl database prior to upgrade?",
                PathHeader = "BACKUP LOCATION",
                YesButtonText  = "Backup",
                NoButtonText = "Skip",
                ValidateFolderIsEmpty = true
            };
            var backupDialogResult = windowManager.ShowOverlayDialog(backupDialog);
            if (backupDialogResult == null)
            {
                //Dialog was cancelled
                eventAggregator.PublishOnUIThread(new RefreshInstances());
                return;
            }
            upgradeOptions.BackupPath = backupDialog.Path;
            upgradeOptions.BackupRavenDbBeforeUpgrade = backupDialogResult.Value; 
           
            var confirm = instance.Service.Status == ServiceControllerStatus.Stopped ||
                          windowManager.ShowYesNoDialog($"STOP INSTANCE AND UPGRADE TO {installer.ZipInfo.Version}", $"{model.Name} needs to be stopped in order to upgrade to version {installer.ZipInfo.Version}.", "Do you want to proceed?", "Yes I want to proceed", "No");
            
            if (confirm)
            {
                using (var progress = model.GetProgressObject($"UPGRADING {model.Name}"))
                {
                    var reportCard = new ReportCard();
                    var restartAgain = model.IsRunning;

                    var stopped = await model.StopService(progress);

                    if (!stopped)
                    {
                        eventAggregator.PublishOnUIThread(new RefreshInstances());
                        reportCard.Errors.Add("Failed to stop the service");
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
                           var serviceStarted =  await model.StartService(progress);
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
        }
    }
}