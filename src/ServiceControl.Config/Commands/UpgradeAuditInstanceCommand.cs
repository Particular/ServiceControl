namespace ServiceControl.Config.Commands
{
    using System;
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
            IServiceControlWindowManager windowManager,
            IEventAggregator eventAggregator,
            ServiceControlAuditInstanceInstaller serviceControlAuditInstaller,
            CommandChecks commandChecks)
        {
            this.windowManager = windowManager;
            this.eventAggregator = eventAggregator;
            this.serviceControlAuditInstaller = serviceControlAuditInstaller;
            this.commandChecks = commandChecks;
        }

        [FeatureToggle(Feature.LicenseChecks)]
        public bool LicenseChecks { get; set; }

        public override async Task ExecuteAsync(InstanceDetailsViewModel model)
        {
            var instance = InstanceFinder.FindInstanceByName<ServiceControlAuditInstance>(model.Name);
            instance.Service.Refresh();

            if (!await commandChecks.CanUpgradeInstance(instance, LicenseChecks))
            {
                return;
            }

            var upgradeOptions = new ServiceControlUpgradeOptions();

            if (!instance.AppConfig.AppSettingExists(AuditInstanceSettingsList.EnableFullTextSearchOnBodies.Name))
            {
                var dialogResult = await windowManager.ShowYesNoCancelDialog(
                    "INPUT REQUIRED - FULL TEXT SEARCH ON MESSAGE BODIES",
                    "ServiceControl Audit indexes message bodies to enable searching for messages by their contents in ServiceInsight. This has a performance impact on the ServiceControl Audit instance and the feature can be disabled if it is not required.", "Do you want to disable full text search for message bodies?", "YES", "NO");
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

            if (await commandChecks.StopBecauseInstanceIsRunning(instance, model.Name))
            {
                return;
            }

            await UpgradeAuditInstance(model, instance, upgradeOptions);

            await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
        }

        async Task UpgradeAuditInstance(
            InstanceDetailsViewModel model,
            ServiceControlAuditInstance instance,
            ServiceControlUpgradeOptions upgradeOptions
            )
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

                reportCard = await Task.Run(() => serviceControlAuditInstaller.Upgrade(instance, upgradeOptions, progress));

                if (reportCard.HasErrors || reportCard.HasWarnings)
                {
                    await windowManager.ShowActionReport(reportCard, "ISSUES UPGRADING INSTANCE", "Could not upgrade instance because of the following errors:", "There were some warnings while upgrading the instance:");
                    return;
                }

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

        readonly IEventAggregator eventAggregator;
        readonly IServiceControlWindowManager windowManager;
        readonly ServiceControlAuditInstanceInstaller serviceControlAuditInstaller;
        readonly CommandChecks commandChecks;
    }
}
