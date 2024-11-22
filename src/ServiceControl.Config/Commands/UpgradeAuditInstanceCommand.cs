namespace ServiceControl.Config.Commands
{
    using System;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using Events;
    using Framework;
    using Framework.Commands;
    using Framework.Modules;
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
            ScmuCommandChecks commandChecks)
        {
            this.windowManager = windowManager;
            this.eventAggregator = eventAggregator;
            this.serviceControlAuditInstaller = serviceControlAuditInstaller;
            this.commandChecks = commandChecks;
        }

        public override async Task ExecuteAsync(InstanceDetailsViewModel model)
        {
            var instance = InstanceFinder.FindInstanceByName<ServiceControlAuditInstance>(model.Name);
            instance.Service.Refresh();

            if (!await commandChecks.CanUpgradeInstance(instance))
            {
                return;
            }

            var upgradeOptions = new ServiceControlUpgradeOptions();

            if (await commandChecks.StopBecauseInstanceIsRunning(instance))
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
        readonly ScmuCommandChecks commandChecks;
    }
}