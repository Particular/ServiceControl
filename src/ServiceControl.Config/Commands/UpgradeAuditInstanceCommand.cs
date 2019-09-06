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
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.ReportCard;
    using UI.InstanceDetails;

    class UpgradeAuditInstanceCommand : AwaitableAbstractCommand<InstanceDetailsViewModel>
    {
        public UpgradeAuditInstanceCommand(Func<InstanceDetailsViewModel, bool> canExecuteMethod = null) : base(canExecuteMethod)
        {
        }

        public UpgradeAuditInstanceCommand(
            IWindowManagerEx windowManager,
            IEventAggregator eventAggregator,
            ServiceControlInstanceInstaller serviceControlInstaller,
            ServiceControlAuditInstanceInstaller serviceControlAuditInstaller)
        {
            this.windowManager = windowManager;
            this.eventAggregator = eventAggregator;
            this.serviceControlInstaller = serviceControlInstaller;
            this.serviceControlAuditInstaller = serviceControlAuditInstaller;
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
                    windowManager.ShowMessage("LICENSE ERROR", $"Upgrade could not continue due to an issue with the current license. {licenseCheckResult.Message}.  Contact contact@particular.net", hideCancel: true);
                    return;
                }
            }

            var instance = InstanceFinder.FindInstanceByName<ServiceControlAuditInstance>(model.Name);
            instance.Service.Refresh();

            var upgradeInfo = UpgradeControl.GetUpgradeInfoForTargetVersion(serviceControlInstaller.ZipInfo.Version, instance.Version);
            var upgradeOptions = new ServiceControlUpgradeOptions {UpgradeInfo = upgradeInfo};

            if (instance.Service.Status != ServiceControllerStatus.Stopped &&
                !windowManager.ShowYesNoDialog($"STOP INSTANCE AND UPGRADE TO {serviceControlInstaller.ZipInfo.Version}",
                    $"{model.Name} needs to be stopped in order to upgrade to version {serviceControlInstaller.ZipInfo.Version}.",
                    "Do you want to proceed?",
                    "Yes, I want to proceed", "No"))
            {
                return;
            }

            await UpgradeAuditInstance(model, instance, upgradeOptions);

            eventAggregator.PublishOnUIThread(new RefreshInstances());
        }

        async Task UpgradeAuditInstance(InstanceDetailsViewModel model, ServiceControlAuditInstance instance, ServiceControlUpgradeOptions upgradeOptions)
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

                reportCard = await Task.Run(() => serviceControlAuditInstaller.Upgrade(instance, upgradeOptions, progress));

                if (reportCard.HasErrors || reportCard.HasWarnings)
                {
                    windowManager.ShowActionReport(reportCard, "ISSUES UPGRADING INSTANCE", "Could not upgrade instance because of the following errors:", "There were some warnings while upgrading the instance:");
                    return;
                }

                if (restartAgain)
                {
                    var serviceStarted = await model.StartService(progress);
                    if (!serviceStarted)
                    {
                        reportCard.Errors.Add("The Service failed to start. Please consult the ServiceControl logs for this instance");
                        windowManager.ShowActionReport(reportCard, "UPGRADE FAILURE", "Instance reported this error after upgrade:");
                    }
                }
            }
        }

        readonly IEventAggregator eventAggregator;
        readonly IWindowManagerEx windowManager;
        readonly ServiceControlInstanceInstaller serviceControlInstaller;
        readonly ServiceControlAuditInstanceInstaller serviceControlAuditInstaller;
    }
}