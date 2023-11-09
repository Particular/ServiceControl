namespace ServiceControl.Config.Commands
{
    using System;
    using System.Diagnostics;
    using System.ServiceProcess;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using Events;
    using Framework;
    using Framework.Commands;
    using Framework.Modules;
    using ServiceControl.Engine.Extensions;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.ReportCard;
    using ServiceControlInstaller.Engine.Validation;
    using UI.InstanceDetails;

    class UpgradeAuditInstanceCommand : AwaitableAbstractCommand<InstanceDetailsViewModel>
    {
        public UpgradeAuditInstanceCommand(Func<InstanceDetailsViewModel, bool> canExecuteMethod = null) : base(canExecuteMethod)
        {
        }

        public UpgradeAuditInstanceCommand(
            IServiceControlWindowManager windowManager,
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
                    await windowManager.ShowMessage("LICENSE ERROR", $"Upgrade could not continue due to an issue with the current license. {licenseCheckResult.Message}.  Contact contact@particular.net", hideCancel: true);
                    return;
                }
            }

            var instance = InstanceFinder.FindInstanceByName<ServiceControlAuditInstance>(model.Name);
            instance.Service.Refresh();

            var compatibleStorageEngine = instance.PersistenceManifest.Name == StorageEngineNames.RavenDB;

            if (!compatibleStorageEngine)
            {
                var upgradeGuide4to5url = "https://docs.particular.net/servicecontrol/upgrades/4to5/";

                var openUpgradeGuide = await windowManager.ShowYesNoDialog("STORAGE ENGINE INCOMPATIBLE",
                    $"Please note that the storage format has changed and the {instance.PersistenceManifest.DisplayName} storage engine is no longer available. Upgrading requires a side-by-side deployment of both versions. Migration guidance is available in the version 4 to 5 upgrade guidance at {upgradeGuide4to5url}",
                    "Open online ServiceControl 4 to 5 upgrade guide in system default browser?",
                    "Yes",
                    "No"
                );

                if (openUpgradeGuide)
                {
                    Process.Start(new ProcessStartInfo(upgradeGuide4to5url) { UseShellExecute = true });
                }

                return;
            }

            var upgradeInfo = UpgradeInfo.GetUpgradePathFor(instance.Version);
            if (upgradeInfo.HasIncompatibleVersion)
            {
                var nextVersion = upgradeInfo.UpgradePath[0];
                await windowManager.ShowMessage("VERSION UPGRADE INCOMPATIBLE",
                    "<Section xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xml:space=\"preserve\" TextAlignment=\"Left\" LineHeight=\"Auto\" IsHyphenationEnabled=\"False\" xml:lang=\"en-us\">\r\n" +
                    $"<Paragraph>You must upgrade to version(s) {upgradeInfo} before upgrading to version {serviceControlInstaller.ZipInfo.Version}:</Paragraph>\r\n" +
                    "<List MarkerStyle=\"Decimal\" Margin=\"0,0,0,0\" Padding=\"0,0,0,0\">\r\n" +
                    $"<ListItem Margin=\"48,0,0,0\"><Paragraph>Download and install version {nextVersion} from https://github.com/Particular/ServiceControl/releases/tag/{nextVersion}</Paragraph></ListItem>" +
                    $"<ListItem Margin=\"48,0,0,0\"><Paragraph>Upgrade this instance to version {nextVersion}.</Paragraph></ListItem>\r\n" +
                    "<ListItem Margin=\"48,0,0,0\"><Paragraph>Download and install the latest version from https://particular.net/start-servicecontrol-download</Paragraph></ListItem>\r\n" +
                    "<ListItem Margin=\"48,0,0,0\"><Paragraph>Upgrade this instance to the latest version of ServiceControl.</Paragraph></ListItem>\r\n" +
                    "</List>\r\n" +
                    "</Section>",
                    hideCancel: true);

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

            if (DotnetVersionValidator.FrameworkRequirementsAreMissing(needsRavenDB: true, out var missingMessage))
            {
                await windowManager.ShowMessage("Missing prerequisites", missingMessage, acceptText: "Cancel", hideCancel: true);
                return;
            }

            if (instance.TransportPackage.IsOldRabbitMQTransport() &&
                !await windowManager.ShowYesNoDialog("UPGRADE WARNING", $"ServiceControl version {serviceControlInstaller.ZipInfo.Version} requires RabbitMQ broker version 3.10.0 or higher. Also, the stream_queue and quorum_queue feature flags must be enabled on the broker. Please confirm your broker meets the minimum requirements before upgrading.",
                   "Do you want to proceed?",
                   "Yes, my RabbitMQ broker meets the minimum requirements",
                   "No, cancel the upgrade"))
            {
                return;
            }

            if (instance.Service.Status != ServiceControllerStatus.Stopped &&
                !await windowManager.ShowYesNoDialog($"STOP INSTANCE AND UPGRADE TO {serviceControlInstaller.ZipInfo.Version}",
                    $"{model.Name} needs to be stopped in order to upgrade to version {serviceControlInstaller.ZipInfo.Version}.",
                    "Do you want to proceed?",
                    "Yes, I want to proceed", "No"))
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
        readonly ServiceControlInstanceInstaller serviceControlInstaller;
        readonly ServiceControlAuditInstanceInstaller serviceControlAuditInstaller;
    }
}
