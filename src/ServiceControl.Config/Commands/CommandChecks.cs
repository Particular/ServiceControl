namespace ServiceControl.Config.Commands
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.ServiceProcess;
    using System.Threading.Tasks;
    using ServiceControl.Config.Framework;
    using ServiceControl.LicenseManagement;
    using ServiceControlInstaller.Engine;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Validation;

    public class CommandChecks : AbstractCommandChecks
    {
        readonly IServiceControlWindowManager windowManager;

        public CommandChecks(IServiceControlWindowManager windowManager)
        {
            this.windowManager = windowManager;
        }

        protected override async Task<bool> PromptForRabbitMqCheck(bool isUpgrade)
        {
            var title = isUpgrade ? "UPGRADE WARNING" : "INSTALL WARNING";
            var beforeWhat = isUpgrade ? "upgrading" : "installing";
            var message = $"ServiceControl version {Constants.CurrentVersion} requires RabbitMQ broker version 3.10.0 or higher. Also, the stream_queue and quorum_queue feature flags must be enabled on the broker. Please confirm your broker meets the minimum requirements before {beforeWhat}.";
            var question = "Do you want to proceed?";
            var yes = "Yes, my RabbitMQ broker meets the minimum requirements";
            var no = "No, cancel the install";

            var continueInstall = await windowManager.ShowYesNoDialog(title, message, question, yes, no);
            return continueInstall;
        }

        protected override Task NotifyForDeprecatedMessageTransport(TransportInfo transport)
        {
            return windowManager.ShowMessage("DEPRECATED MESSAGE TRANSPORT", $"The message transport '{transport.DisplayName}' is not available in this version of ServiceControl, and this instance cannot be upgraded.", acceptText: "Cancel Upgrade", hideCancel: true);
        }

        protected override Task NotifyForMissingSystemPrerequisites(string missingPrereqsMessage)
        {
            return windowManager.ShowMessage("Missing prerequisites", missingPrereqsMessage, acceptText: "Cancel", hideCancel: true);
        }

        protected override async Task NotifyForIncompatibleStorageEngine(IServiceControlBaseInstance baseInstance)
        {
            var openUpgradeGuide = await windowManager.ShowYesNoDialog("STORAGE ENGINE INCOMPATIBLE",
                $"Please note that the storage format has changed and the {baseInstance.PersistenceManifest.DisplayName} storage engine is no longer available. Upgrading requires a side-by-side deployment of both versions. Migration guidance is available in the version 4 to 5 upgrade guidance at {UpgradeGuide4to5Url}",
                "Open online ServiceControl 4 to 5 upgrade guide in system default browser?",
                "Yes",
                "No"
            );

            if (openUpgradeGuide)
            {
                Process.Start(new ProcessStartInfo(UpgradeGuide4to5Url) { UseShellExecute = true });
            }
        }

        protected override async Task NotifyForIncompatibleUpgradeVersion(UpgradeInfo upgradeInfo)
        {
            var nextVersion = upgradeInfo.UpgradePath[0];
            await windowManager.ShowMessage("VERSION UPGRADE INCOMPATIBLE",
                "<Section xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xml:space=\"preserve\" TextAlignment=\"Left\" LineHeight=\"Auto\" IsHyphenationEnabled=\"False\" xml:lang=\"en-us\">\r\n" +
                $"<Paragraph>You must upgrade to version(s) {upgradeInfo} before upgrading to version {Constants.CurrentVersion}:</Paragraph>\r\n" +
                "<List MarkerStyle=\"Decimal\" Margin=\"0,0,0,0\" Padding=\"0,0,0,0\">\r\n" +
                $"<ListItem Margin=\"48,0,0,0\"><Paragraph>Download and install version {nextVersion} from https://github.com/Particular/ServiceControl/releases/tag/{nextVersion}</Paragraph></ListItem>" +
                $"<ListItem Margin=\"48,0,0,0\"><Paragraph>Upgrade this instance to version {nextVersion}.</Paragraph></ListItem>\r\n" +
                "<ListItem Margin=\"48,0,0,0\"><Paragraph>Download and install the latest version from https://particular.net/start-servicecontrol-download</Paragraph></ListItem>\r\n" +
                "<ListItem Margin=\"48,0,0,0\"><Paragraph>Upgrade this instance to the latest version of ServiceControl.</Paragraph></ListItem>\r\n" +
                "</List>\r\n" +
                "</Section>",
                hideCancel: true);
        }

        protected override Task NotifyForLicenseIssue(string licenseMessage)
        {
            return windowManager.ShowMessage("LICENSE ERROR", licenseMessage, hideCancel: true);
        }

        protected override Task<bool> PromptToStopRunningInstance(BaseService instance)
        {
            return windowManager.ShowYesNoDialog($"STOP INSTANCE AND UPGRADE TO {Constants.CurrentVersion}",
                $"{instance.Name} needs to be stopped in order to upgrade to version {Constants.CurrentVersion}.",
                "Do you want to proceed?",
                "Yes, I want to proceed", "No");
        }
    }

    public abstract class AbstractCommandChecks
    {
        protected const string UpgradeGuide4to5Url = "https://docs.particular.net/servicecontrol/upgrades/4to5/";

        protected abstract Task<bool> PromptForRabbitMqCheck(bool isUpgrade);
        protected abstract Task<bool> PromptToStopRunningInstance(BaseService instance);
        protected abstract Task NotifyForDeprecatedMessageTransport(TransportInfo transport);
        protected abstract Task NotifyForMissingSystemPrerequisites(string missingPrereqsMessage);
        protected abstract Task NotifyForIncompatibleStorageEngine(IServiceControlBaseInstance baseInstance);
        protected abstract Task NotifyForIncompatibleUpgradeVersion(UpgradeInfo upgradeInfo);
        protected abstract Task NotifyForLicenseIssue(string licenseMessage);

        public async Task<bool> CanAddInstance(bool needsRavenDB)
        {
            // Check for license
            if (!await IsLicenseOk())
            {
                return false;
            }

            if (await FrameworkRequirementsAreMissing(needsRavenDB))
            {
                return false;
            }

            return true;
        }

        public async Task<bool> ValidateNewInstance(params IServiceInstance[] instances)
        {
            var transport = instances
                .OfType<ITransportConfig>()
                .Where(i => i is not null)
                .Select(i => i.TransportPackage)
                .First(t => t is not null);

            var continueInstall = await RabbitMqCheckIsOK(transport, false);

            return continueInstall;
        }

        async Task<bool> RabbitMqCheckIsOK(TransportInfo transport, bool isUpgrade)
        {
            if (transport is null)
            {
                throw new ArgumentNullException(nameof(transport));
            }

            if (transport.ZipName != "RabbitMQ")
            {
                // not Rabbit, don't care
                return true;
            }

            // Only way we DON'T need to warn is if we're updating an instance that's already on a "new" (AvailableInSCMU) Rabbit transport
            var needToWarn = !(isUpgrade && transport.AvailableInSCMU);
            if (!needToWarn)
            {
                return true;
            }

            return await PromptForRabbitMqCheck(isUpgrade);
        }

        public async Task<bool> CanUpgradeInstance(BaseService instance, bool forceUpgradeDb = false)
        {
            // Check for license
            if (!await IsLicenseOk())
            {
                return false;
            }

            // Check for transports that can't be upgraded
            var cantUpdateTransport = instance.TransportPackage.Removed && instance.TransportPackage.AutoMigrateTo is null;
            if (cantUpdateTransport)
            {
                await NotifyForDeprecatedMessageTransport(instance.TransportPackage);
                return false;
            }

            // Validate .NET Framework requirements
            bool needsRavenDB = instance is IServiceControlBaseInstance;
            if (await FrameworkRequirementsAreMissing(needsRavenDB))
            {
                return false;
            }

            // RavenDB 5+ check
            if (instance is IServiceControlBaseInstance baseInstance)
            {
                if (!forceUpgradeDb)
                {
                    var compatibleStorageEngine = baseInstance.PersistenceManifest.Name == StorageEngineNames.RavenDB;

                    if (!compatibleStorageEngine)
                    {
                        await NotifyForIncompatibleStorageEngine(baseInstance);
                        return false;
                    }
                }

                // To be clear, Monitoring doesn't bother with this check because it's all in-memory storage
                // so you could hypothetically change to any version at any time
                var upgradeInfo = UpgradeInfo.GetUpgradePathFor(instance.Version);
                if (upgradeInfo.HasIncompatibleVersion)
                {
                    await NotifyForIncompatibleUpgradeVersion(upgradeInfo);
                    return false;
                }
            }

            if (!await RabbitMqCheckIsOK(instance.TransportPackage, isUpgrade: true))
            {
                return false;
            }

            return true;
        }

        async Task<bool> FrameworkRequirementsAreMissing(bool needsRavenDB)
        {
            if (DotnetVersionValidator.FrameworkRequirementsAreMissing(needsRavenDB, out var missingMessage))
            {
                await NotifyForMissingSystemPrerequisites(missingMessage);
                return true;
            }

            return false;
        }

        async Task<bool> IsLicenseOk()
        {
            var licenseCheckResult = CheckLicenseIsValid();
            if (!licenseCheckResult.Valid)
            {
                await NotifyForLicenseIssue($"Upgrade could not continue due to an issue with the current license. {licenseCheckResult.Message}.  Contact contact@particular.net");
                return false;
            }

            return true;
        }

        public async Task<bool> StopBecauseInstanceIsRunning(BaseService instance)
        {
            if (instance.Service.Status == ServiceControllerStatus.Stopped)
            {
                return false;
            }

            var proceed = await PromptToStopRunningInstance(instance);

            return !proceed;
        }

        CheckLicenseResult CheckLicenseIsValid()
        {
            var license = LicenseManager.FindLicense();

            if (license.Details.HasLicenseExpired())
            {
                return new CheckLicenseResult(false, "License has expired");
            }

            if (!license.Details.ValidForServiceControl)
            {
                return new CheckLicenseResult(false, "This license edition does not include ServiceControl");
            }

            var releaseDate = LicenseManager.GetReleaseDate();

            if (license.Details.ReleaseNotCoveredByMaintenance(releaseDate))
            {
                return new CheckLicenseResult(false, "License does not cover this release of ServiceControl Monitoring. Upgrade protection expired.");
            }

            return new CheckLicenseResult(true);
        }

        class CheckLicenseResult
        {
            public CheckLicenseResult(bool valid, string message = null)
            {
                Valid = valid;
                Message = message;
            }

            public bool Valid { get; private set; }
            public string Message { get; private set; }
        }
    }
}
