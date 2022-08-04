namespace ServiceControl.Config.Commands
{
    using System;
    using System.ServiceProcess;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using Events;
    using Framework;
    using Framework.Commands;
    using Framework.Modules;
    using ServiceControl.Config.Extensions;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.ReportCard;
    using UI.InstanceDetails;

    class UpgradeMonitoringInstanceCommand : AwaitableAbstractCommand<InstanceDetailsViewModel>
    {
        public UpgradeMonitoringInstanceCommand(Func<InstanceDetailsViewModel, bool> canExecuteMethod = null) : base(canExecuteMethod)
        {
        }

        public UpgradeMonitoringInstanceCommand(IServiceControlWindowManager windowManager, IEventAggregator eventAggregator, MonitoringInstanceInstaller installer)
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
                    await windowManager.ShowMessage("LICENSE ERROR", $"Upgrade could not continue due to an issue with the current license. {licenseCheckResult.Message}.  Contact contact@particular.net", hideCancel: true);
                    return;
                }
            }

            var instance = InstanceFinder.FindMonitoringInstance(model.Name);


            instance.Service.Refresh();

            if (instance.TransportPackage.IsOldRabbitMQTransport() &&
                !await windowManager.ShowYesNoDialog("UPGRADE WARNING", $"ServiceControl version {installer.ZipInfo.Version} requires RabbitMQ broker version 3.10.0 or higher. Also, the stream_queue and quorum_queue feature flags must be enabled on the broker. Please confirm your broker meets the minimum requirements before upgrading.",
                   "Do you want to proceed?",
                   "Yes, my RabbitMQ broker meets the minimum requirements",
                   "No, cancel the upgrade"))
            {
                return;
            }

            var confirm = instance.Service.Status == ServiceControllerStatus.Stopped ||
                          await windowManager.ShowYesNoDialog($"STOP INSTANCE AND UPGRADE TO {installer.ZipInfo.Version}", $"{model.Name} needs to be stopped in order to upgrade to version {installer.ZipInfo.Version}.", "Do you want to proceed?", "Yes, I want to proceed", "No");

            if (confirm)
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

                    reportCard = await Task.Run(() => installer.Upgrade(model.Name, progress));

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
                                reportCard.Errors.Add("The Service failed to start. Please consult the  logs for this instance");
                                await windowManager.ShowActionReport(reportCard, "UPGRADE FAILURE", "Instance reported this error after upgrade:");
                            }
                        }
                    }
                }

                await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
            }
        }

        readonly IEventAggregator eventAggregator;
        readonly MonitoringInstanceInstaller installer;
        readonly IServiceControlWindowManager windowManager;
    }
}