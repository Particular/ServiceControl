namespace ServiceControl.Config.Commands
{
    using System;
    using System.Reactive.Linq;
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

    class UpgradeInstanceCommand : AwaitableAbstractCommand<InstanceDetailsViewModel>
    {
        private readonly IEventAggregator eventAggregator;
        private readonly Installer installer;
        private readonly IWindowManagerEx windowManager;

        public UpgradeInstanceCommand(IWindowManagerEx windowManager, IEventAggregator eventAggregator, Installer installer)
        {
            this.windowManager = windowManager;

            this.eventAggregator = eventAggregator;
            this.installer = installer;
        }

        public override async Task ExecuteAsync(InstanceDetailsViewModel instanceViewModel)
        {
            var instance = ServiceControlInstance.FindByName(instanceViewModel.Name);
            instance.Service.Refresh();

            var upgradeOptions = new InstanceUpgradeOptions();
            
            if (!instance.AppSettingExists(SettingsList.ForwardErrorMessages.Name))
            {
                var result  = windowManager.ShowYesNoCancelDialog("UPGRADE QUESTION - DISABLE ERROR FORWARDING", "Error messages can be forwarded to a secondary error queue known as the Error Forwarding Queue.  This queue exists to allow external tools to receive error messages. If you do not have a tool processing messages from the Error Forwarding Queue this setting should be disabled.", "Disable", "Enable");
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
            }

            if (!upgradeOptions.AuditRetentionPeriod.HasValue)
            {
                var viewModel = new SliderDialogViewModel("UPGRADE QUESTION - DATABASE RETENTION",
                    "Service Control periodically purges audit messages from the database.",
                    "AUDIT RETENTION PERIOD",
                    "Please specify the age at which these records should be removed",
                    TimeSpanUnits.Hours,
                    1, 
                    (int)Math.Truncate(TimeSpan.FromDays(365).TotalHours),
                    1,
                    5,
                    TimeSpan.FromDays(30));

                if (windowManager.ShowSliderDialog(viewModel))
                {
                    upgradeOptions.AuditRetentionPeriod = new TimeSpan(viewModel.Period.Days, viewModel.Period.Hours, 0, 0);
                }
            }

            
            if (!instance.AppSettingExists(SettingsList.ErrorRetentionPeriod.Name))
            {
                var viewModel = new SliderDialogViewModel("UPGRADE QUESTION - DATABASE RETENTION",
                        "Service Control periodically purges resolved and archived error messages from the database.",
                        "ERROR RETENTION PERIOD",
                        "Please specify the age at which these records should be removed",
                        TimeSpanUnits.Days,
                        10,
                        50, 
                        1,
                        5, 
                        TimeSpan.FromDays(15));

                if (windowManager.ShowSliderDialog(viewModel))
                {
                    upgradeOptions.ErrorRetentionPeriod = TimeSpan.FromHours(Math.Truncate(viewModel.Period.TotalHours));
                }
            }
            
            var confirm = instance.Service.Status == ServiceControllerStatus.Stopped ||
            windowManager.ShowMessage(string.Format("STOP INSTANCE AND UPGRADE TO {0}", installer.ZipInfo.Version), string.Format("{0} needs to be stopped in order to upgrade to version {1}. Do you want to proceed?", instanceViewModel.Name, installer.ZipInfo.Version));
            
            if (confirm)
            {
                using (var progress = instanceViewModel.GetProgressObject("UPGRADING " + instanceViewModel.Name))
                {
                    instance.Service.Refresh();
                    var isRunning = instance.Service.Status == ServiceControllerStatus.Running;
                    if (isRunning) await instanceViewModel.StopService();

                    var reportCard = await Task.Run(() => installer.Upgrade(instanceViewModel.Name, upgradeOptions, progress));

                    if (reportCard.HasErrors || reportCard.HasWarnings)
                    {
                        windowManager.ShowActionReport(reportCard, "ISSUES UPGRADING INSTANCE", "Could not upgrade instance because of the following errors:", "There were some warnings while upgrading  the instance:");
                    }
                    else
                    {
                        if (isRunning) await instanceViewModel.StartService();
                    }
                }
            }

            eventAggregator.PublishOnUIThread(new RefreshInstances());
        }
    }
}