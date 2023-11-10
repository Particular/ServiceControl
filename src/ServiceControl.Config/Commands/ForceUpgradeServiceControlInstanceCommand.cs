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

class ForceUpgradeServiceControlInstanceCommand : AwaitableAbstractCommand<ServiceControlAdvancedViewModel>
{
    public ForceUpgradeServiceControlInstanceCommand(
        IServiceControlWindowManager windowManager,
        IEventAggregator eventAggregator,
        ServiceControlInstanceInstaller serviceControlInstaller,
        CommandChecks commandChecks) : base(ForcedUpgradeAllowed)
    {
        this.windowManager = windowManager;
        this.eventAggregator = eventAggregator;
        this.serviceControlInstaller = serviceControlInstaller;
        this.commandChecks = commandChecks;
    }

    [FeatureToggle(Feature.LicenseChecks)]
    public bool LicenseChecks { get; set; }

    static bool ForcedUpgradeAllowed(ServiceControlAdvancedViewModel model)
    {
        var instance = InstanceFinder.ServiceControlInstances().FirstOrDefault(i => i.Name == model.Name);

        //HINT: Force upgrade is available only primary v4 instance, running on RavenDB 3.5
        return instance != null && instance.Version.Major == 4 && instance.PersistenceManifest.Name != StorageEngineNames.RavenDB;
    }
    public override async Task ExecuteAsync(ServiceControlAdvancedViewModel model)
    {
        var instance = InstanceFinder.FindInstanceByName<ServiceControlInstance>(model.Name);
        instance.Service.Refresh();

        if (!await commandChecks.CanUpgradeInstance(instance, LicenseChecks, forceUpgradeDb: true))
        {
            return;
        }

        if (await windowManager.ShowMessage("Forced migration",
                "Do you want to proceed with forced migration to version 5?", "Yes") == false)
        {
            return;
        }

        if (!ForcedUpgradeAllowed(model))
        {
            await windowManager.ShowMessage("Cannot run the command", "Only ver. 4.x primary instance that use RavenDB ver. 3.5 can be forced upgraded.");

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
                //HINT: we move the data directory to a backup location
                Directory.Move(instance.DBPath, model.ForcedUpgradeBackupLocation);

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
    readonly CommandChecks commandChecks;
}