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
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.ReportCard;

    class UpgradeMonitoringInstanceCommand : AwaitableAbstractCommand<InstanceDetailsViewModel>
    {
        private readonly IEventAggregator eventAggregator;
        private readonly MonitoringInstanceInstaller installer;
        private readonly IWindowManagerEx windowManager;

        public UpgradeMonitoringInstanceCommand(Func<InstanceDetailsViewModel, bool> canExecuteMethod = null) : base(canExecuteMethod)
        {
        }

        public UpgradeMonitoringInstanceCommand(IWindowManagerEx windowManager, IEventAggregator eventAggregator,MonitoringInstanceInstaller installer)
        {
            this.windowManager = windowManager;
            this.eventAggregator = eventAggregator;
            this.installer = installer;
        }

        public override async Task ExecuteAsync(InstanceDetailsViewModel model)
        {
            var licenseCheckResult = installer.CheckLicenseIsValid();
            if (!licenseCheckResult.Valid)
            {
                windowManager.ShowMessage("LICENSE ERROR", $"Upgrade could not continue due to an issue with the current license. {licenseCheckResult.Message}.  Contact sales@particular.net", hideCancel: true);
                return;
            }
            
            var instance = InstanceFinder.FindMonitoringInstance(model.Name);
            

            instance.Service.Refresh();
            
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
                        reportCard.SetStatus();
                        windowManager.ShowActionReport(reportCard, "ISSUES UPGRADING INSTANCE", "Could not upgrade instance because of the following errors:");

                        return;
                    }

                    reportCard = await Task.Run(() => installer.Upgrade(model.Name, progress));

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
                                reportCard.Errors.Add("The Service failed to start. Please consult the  logs for this instance");
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