namespace ServiceControl.Config.Commands
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using ServiceControl.Config.Framework;
    using ServiceControlInstaller.Engine;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
    using ServiceControlInstaller.Engine.Instances;

    public class ScmuCommandChecks : AbstractCommandChecks
    {
        readonly IServiceControlWindowManager windowManager;

        public ScmuCommandChecks(IServiceControlWindowManager windowManager)
        {
            this.windowManager = windowManager;
        }

        protected override async Task<bool> PromptForRabbitMqCheck(bool isUpgrade)
        {
            var title = isUpgrade ? "UPGRADE WARNING" : "INSTALL WARNING";
            var beforeWhat = isUpgrade ? "upgrading" : "installing";
            var message = $"ServiceControl version {Constants.CurrentVersion} requires:\n• RabbitMQ broker version 3.10.0 or higher\n• The stream_queue and quorum_queue feature flags must be enabled\n• The management plugin must be enabled\n\nPlease confirm your broker meets the minimum requirements before {beforeWhat}.";
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
                $"The storage format has changed and the {baseInstance.PersistenceManifest.DisplayName} storage engine is no longer available. Upgrading requires a side-by-side deployment of both versions. Migration guidance is available in the version 4 to 5 upgrade guidance at {UpgradeGuide4to5Url}",
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

        protected override Task NotifyError(string title, string message)
        {
            return windowManager.ShowMessage(title.ToUpperInvariant(), message, hideCancel: true);
        }

        protected override Task<bool> PromptToStopRunningInstance(BaseService instance)
        {
            return windowManager.ShowYesNoDialog($"STOP INSTANCE AND UPGRADE TO {Constants.CurrentVersion}",
                $"{instance.Name} needs to be stopped in order to upgrade to version {Constants.CurrentVersion}.",
                "Do you want to proceed?",
                "Yes, I want to proceed", "No");
        }

        protected override Task<bool> PromptToContinueWithForcedUpgrade()
        {
            return windowManager.ShowMessage("Forced migration",
                "Do you want to proceed with forced migration to ServiceControl 5? The current RavenDB 3.5 database will be moved aside and a new database will be created.", "Yes");
        }
    }
}
