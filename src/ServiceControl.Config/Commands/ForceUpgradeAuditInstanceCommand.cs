namespace ServiceControl.Config.Commands;

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Caliburn.Micro;
using Events;
using Framework;
using Framework.Commands;
using Framework.Modules;
using ServiceControlInstaller.Engine.Instances;
using ServiceControlInstaller.Engine.ReportCard;
using UI.AdvancedOptions;

class ForceUpgradeAuditInstanceCommand : AwaitableAbstractCommand<ServiceControlAdvancedViewModel>
{
    public ForceUpgradeAuditInstanceCommand(
        IServiceControlWindowManager windowManager,
        IEventAggregator eventAggregator,
        ServiceControlAuditInstanceInstaller serviceControlAuditInstaller,
        CommandChecks commandChecks
        ) : base(ForcedUpgradeAllowed)
    {
        this.windowManager = windowManager;
        this.eventAggregator = eventAggregator;
        this.serviceControlAuditInstaller = serviceControlAuditInstaller;
        this.commandChecks = commandChecks;
    }

    static bool ForcedUpgradeAllowed(ServiceControlAdvancedViewModel model)
    {
        var instance = InstanceFinder.ServiceControlAuditInstances().FirstOrDefault(i => i.Name == model.Name);

        //HINT: Force upgrade is available only primary v4 instance, running on RavenDB 3.5
        return instance != null && instance.Version.Major == 4 && instance.PersistenceManifest.Name != StorageEngineNames.RavenDB;
    }
    public override async Task ExecuteAsync(ServiceControlAdvancedViewModel model)
    {
        if (!ForcedUpgradeAllowed(model))
        {
            await windowManager.ShowMessage("Cannot run the command", "Only ServiceControl 4.x primary instances that use RavenDB 3.5 persistence can be force-upgraded.", hideCancel: true);

            return;
        }

        var instance = InstanceFinder.FindInstanceByName<ServiceControlAuditInstance>(model.Name);
        instance.Service.Refresh();

        if (!await commandChecks.CanUpgradeInstance(instance, forceUpgradeDb: true))
        {
            return;
        }

        if (await windowManager.ShowMessage("Forced migration",
                "Do you want to proceed with forced migration to ServiceControl 5? The current RavenDB 3.5 database will be moved aside and a new database will be created.", "Yes") == false)
        {
            return;
        }

        await UpgradeServiceControlInstance(model, instance, new ServiceControlUpgradeOptions());

        await eventAggregator.PublishOnUIThreadAsync(new ResetInstances());
    }

    async Task UpgradeServiceControlInstance(ServiceControlAdvancedViewModel model, ServiceControlAuditInstance instance, ServiceControlUpgradeOptions upgradeOptions)
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
                instance.PersistenceManifest = ServiceControlPersisters.GetAuditPersistence(StorageEngineNames.RavenDB);

                return serviceControlAuditInstaller.Upgrade(instance, upgradeOptions, progress);
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
    readonly ServiceControlAuditInstanceInstaller serviceControlAuditInstaller;
    readonly CommandChecks commandChecks;
}