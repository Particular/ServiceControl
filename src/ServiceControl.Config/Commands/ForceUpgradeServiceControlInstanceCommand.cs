namespace ServiceControl.Config.Commands;

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using Events;
using Framework;
using Framework.Commands;
using Framework.Modules;
using ServiceControlInstaller.Engine.Instances;
using ServiceControlInstaller.Engine.ReportCard;
using UI.AdvancedOptions;

class ForceUpgradePrimaryInstanceCommand : AwaitableAbstractCommand<ServiceControlAdvancedViewModel>
{
    public ForceUpgradePrimaryInstanceCommand(
        IServiceControlWindowManager windowManager,
        IEventAggregator eventAggregator,
        ServiceControlInstanceInstaller serviceControlInstaller,
        ScmuCommandChecks commandChecks)
        : base(null)
    {
        this.windowManager = windowManager;
        this.eventAggregator = eventAggregator;
        this.serviceControlInstaller = serviceControlInstaller;
        this.commandChecks = commandChecks;
    }

    public override async Task ExecuteAsync(ServiceControlAdvancedViewModel model)
    {
        var instance = InstanceFinder.FindInstanceByName<ServiceControlInstance>(model.Name);
        instance.Service.Refresh();

        if (!await commandChecks.CanUpgradeInstance(instance, forceUpgradeDb: true))
        {
            return;
        }

        await UpgradeServiceControlInstance(model, instance, CancellationToken.None);

        await eventAggregator.PublishOnUIThreadAsync(new ResetInstances());
    }

    async Task UpgradeServiceControlInstance(ServiceControlAdvancedViewModel model, ServiceControlInstance instance, CancellationToken cancellationToken)
    {
        using (var progress = model.GetProgressObject($"UPGRADING {model.Name}"))
        {
            var reportCard = new ReportCard();
            var restartAgain = model.IsRunning;

            var stopped = await model.StopService(progress, cancellationToken);

            if (!stopped)
            {
                await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances(), cancellationToken);

                reportCard.Errors.Add("Failed to stop the service");
                reportCard.SetStatus();
                await windowManager.ShowActionReport(reportCard, "ISSUES UPGRADING INSTANCE", "Could not upgrade instance because of the following errors:", cancellationToken: cancellationToken);

                return;
            }

            if (Directory.Exists(model.ForcedUpgradeBackupLocation))
            {
                await windowManager.ShowMessage("Cannot make database backup.", $"The target database backup location: {model.ForcedUpgradeBackupLocation} already exists.", hideCancel: true, cancellationToken: cancellationToken);

                return;
            }

            reportCard = await Task.Run(() =>
            {
                instance.CreateDatabaseBackup();
                instance.PersistenceManifest = ServiceControlPersisters.GetPrimaryPersistence(StorageEngineNames.RavenDB);

                return serviceControlInstaller.Upgrade(instance, new ServiceControlUpgradeOptions(), progress);
            });

            if (reportCard.HasErrors || reportCard.HasWarnings)
            {
                await windowManager.ShowActionReport(reportCard, "ISSUES UPGRADING INSTANCE", "Could not upgrade instance because of the following errors:", "There were some warnings while upgrading the instance:", cancellationToken: cancellationToken);

                return;
            }

            if (restartAgain)
            {
                var serviceStarted = await model.StartService(progress, maintenanceMode: false, cancellationToken);
                if (!serviceStarted)
                {
                    reportCard.Errors.Add(
                        "The Service failed to start. Please consult the ServiceControl logs for this instance");
                    await windowManager.ShowActionReport(reportCard, "UPGRADE FAILURE",
                        "Instance reported this error after upgrade:", cancellationToken: cancellationToken);

                    return;
                }
            }
        }

        await model.TryCloseAsync(true);
        await eventAggregator.PublishOnUIThreadAsync(new ResetInstances(), cancellationToken);
    }

    readonly IEventAggregator eventAggregator;
    readonly IServiceControlWindowManager windowManager;
    readonly ServiceControlInstanceInstaller serviceControlInstaller;
    readonly ScmuCommandChecks commandChecks;
}