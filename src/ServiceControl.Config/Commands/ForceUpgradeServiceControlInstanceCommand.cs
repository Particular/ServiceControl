namespace ServiceControl.Config.Commands;

using System.Linq;
using System.Threading.Tasks;
using Caliburn.Micro;
using Framework.Commands;
using Events;
using Framework.Modules;
using Framework;
using ServiceControlInstaller.Engine.Configuration.ServiceControl;
using ServiceControlInstaller.Engine.FileSystem;
using ServiceControlInstaller.Engine.Instances;
using ServiceControlInstaller.Engine.ReportCard;
using ServiceControlInstaller.Engine.Validation;
using UI.AdvancedOptions;

class ForceUpgradeServiceControlInstanceCommand : AwaitableAbstractCommand<ServiceControlAdvancedViewModel>
{
    public ForceUpgradeServiceControlInstanceCommand(
        IServiceControlWindowManager windowManager,
        IEventAggregator eventAggregator,
        ServiceControlInstanceInstaller serviceControlInstaller) : base(ForcedUpgradeAllowed)
    {
        this.windowManager = windowManager;
        this.eventAggregator = eventAggregator;
        this.serviceControlInstaller = serviceControlInstaller;
    }

    [FeatureToggle(Feature.LicenseChecks)]
    public bool LicenseChecks { get; set; }

    static bool ForcedUpgradeAllowed(ServiceControlAdvancedViewModel model)
    {
        var instance = InstanceFinder.ServiceControlInstances().FirstOrDefault(i => i.Name == model.Name);

        //HINT: Force upgrade is available only primary v4 instance, running on RavenDB 3.5
        return instance != null && instance.Version.Major == 4 && instance.PersistenceManifest.Name == StorageEngineNames.RavenDB35;
    }
    public override async Task ExecuteAsync(ServiceControlAdvancedViewModel model)
    {
        if (LicenseChecks)
        {
            var licenseCheckResult = serviceControlInstaller.CheckLicenseIsValid();
            if (!licenseCheckResult.Valid)
            {
                await windowManager.ShowMessage("LICENSE ERROR", $"Upgrade could not continue due to an issue with the current license. {licenseCheckResult.Message}.  Contact contact@particular.net", hideCancel: true);
                return;
            }
        }

        if (!ForcedUpgradeAllowed(model))
        {
            await windowManager.ShowMessage("Cannot run the command", "Only ver. 4.x primary instance that use RavenDB ver. 3.5 can be forced upgraded.");

            return;
        }

        if (DotnetVersionValidator.FrameworkRequirementsAreMissing(needsRavenDB: true, out var missingMessage))
        {
            await windowManager.ShowMessage("Missing prerequisites", missingMessage, acceptText: "Cancel", hideCancel: true);
            return;
        }

        var instance = InstanceFinder.FindInstanceByName<ServiceControlInstance>(model.Name);

        var upgradeInfo = UpgradeControl.GetUpgradeInfoForTargetVersion(serviceControlInstaller.ZipInfo.Version, instance.Version);
        var upgradeOptions = new ServiceControlUpgradeOptions
        {
            UpgradeInfo = upgradeInfo,
        };

        await UpgradeServiceControlInstance(model, instance, upgradeOptions);

        await eventAggregator.PublishOnUIThreadAsync(new ResetInstances());
    }

    async Task UpgradeServiceControlInstance(ServiceControlAdvancedViewModel model, ServiceControlInstance instance, ServiceControlUpgradeOptions upgradeOptions)
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

            reportCard = await Task.Run(() =>
            {
                //HINT: we wipe out the database before we continue with the upgrade
                FileUtils.DeleteDirectory(instance.DBPath, recursive: true, contentsOnly: true);
                instance.PersistenceManifest = ServiceControlPersisters.PrimaryPersistenceManifests.Single(pm => pm.Name == StorageEngineNames.RavenDB5);

                return serviceControlInstaller.Upgrade(instance, upgradeOptions, progress);
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
}