namespace ServiceControl.Config.Commands;

using System.IO;
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

        await UpgradeServiceControlInstance(model, instance);

        await eventAggregator.PublishOnUIThreadAsync(new ResetInstances());
    }

    async Task UpgradeServiceControlInstance(ServiceControlAdvancedViewModel model, ServiceControlInstance instance)
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

            if (Directory.Exists(model.ForcedUpgradeBackupLocation))
            {
                await windowManager.ShowMessage("Cannot make database backup.", $"The target database backup location: {model.ForcedUpgradeBackupLocation} already exists.", hideCancel: true);

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
                await windowManager.ShowActionReport(reportCard, "ISSUES UPGRADING INSTANCE", "Could not upgrade instance because of the following errors:", "There were some warnings while upgrading the instance:");
            }
            else
            {
                if (restartAgain)
                {
                    var serviceStarted = await model.StartService(progress, maintenanceMode: false);
                    if (!serviceStarted)
                    {
                        reportCard.Errors.Add("The Service failed to start. Please consult the ServiceControl logs for this instance");
                        await windowManager.ShowActionReport(reportCard, "UPGRADE FAILURE", "Instance reported this error after upgrade:");
                    }
                }
            }
        }
    }

    readonly IEventAggregator eventAggregator;
    readonly IServiceControlWindowManager windowManager;
    readonly ServiceControlInstanceInstaller serviceControlInstaller;
    readonly ScmuCommandChecks commandChecks;
}