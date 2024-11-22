namespace ServiceControl.Management.PowerShell.Validation
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Management.Automation;
    using System.Text;
    using System.Threading.Tasks;
    using ServiceControlInstaller.Engine;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
    using ServiceControlInstaller.Engine.Instances;

    class PowerShellCommandChecks : AbstractCommandChecks
    {
        readonly PSCmdlet cmdlet;
        readonly string[] acknowledgements;

        public PowerShellCommandChecks(PSCmdlet cmdlet, string[] acknowledgements = null)
        {
            this.cmdlet = cmdlet;
            this.acknowledgements = acknowledgements ?? Array.Empty<string>();
        }

        [DoesNotReturn]
        void Terminate(string message, string errorId, ErrorCategory category)
        {
            var errorRecord = new ErrorRecord(new Exception(message), errorId, category, null);
            cmdlet.ThrowTerminatingError(errorRecord);
        }

        protected override Task NotifyForDeprecatedMessageTransport(TransportInfo transport)
        {
            var terminateMsg = $"The message transport '{transport.DisplayName}' is not available in this version of ServiceControl, and this instance cannot be upgraded.";
            Terminate(terminateMsg, "Install Error", ErrorCategory.InvalidOperation);
            return Task.CompletedTask;
        }

        protected override Task NotifyForIncompatibleStorageEngine(IServiceControlBaseInstance baseInstance)
        {
            var msg = $"The storage format has changed and the {baseInstance.PersistenceManifest.DisplayName} storage engine is no longer available. Upgrading requires a side-by-side deployment of both versions. Migration guidance is available in the version 4 to 5 upgrade guidance at {UpgradeGuide4to5Url}";
            Terminate(msg, "Install Error", ErrorCategory.InvalidOperation);
            return Task.CompletedTask;
        }

        protected override Task NotifyForIncompatibleUpgradeVersion(UpgradeInfo upgradeInfo)
        {
            var nextVersion = upgradeInfo.UpgradePath[0];
            var b = new StringBuilder();

            b.AppendLine($"You must upgrade to version(s) {upgradeInfo} before upgrading to version {Constants.CurrentVersion}:");
            b.AppendLine($" • Download and install version {nextVersion} from https://github.com/Particular/ServiceControl/releases/tag/{nextVersion}");
            b.AppendLine($" • Upgrade this instance to version {nextVersion}.");
            b.AppendLine($" • Download and install the latest version from https://particular.net/start-servicecontrol-download");
            b.AppendLine($" • Upgrade this instance to the latest version of ServiceControl.");

            Terminate(b.ToString(), "Install Error", ErrorCategory.InvalidOperation);
            return Task.CompletedTask;
        }

        protected override Task NotifyError(string title, string message)
        {
            Terminate(message, title, ErrorCategory.InvalidOperation);
            return Task.CompletedTask;
        }

        protected override Task NotifyForMissingSystemPrerequisites(string missingPrereqsMessage)
        {
            Terminate(missingPrereqsMessage, "Missing Prerequisites", ErrorCategory.NotInstalled);
            return Task.CompletedTask;
        }

        protected override Task<bool> PromptForRabbitMqCheck(bool isUpgrade)
        {
            if (acknowledgements.Any(ack => ack.Equals(AcknowledgementValues.RabbitMQBrokerVersion310, StringComparison.OrdinalIgnoreCase)))
            {
                return Task.FromResult(true);
            }

            var terminateMsg = $"ServiceControl version {Constants.CurrentVersion} requires RabbitMQ broker version 3.10.0 or higher. Also, the stream_queue and quorum_queue feature flags must be enabled on the broker. Use -Acknowledgements {AcknowledgementValues.RabbitMQBrokerVersion310} if you are sure your broker meets these requirements.";

            Terminate(terminateMsg, "Install Error", ErrorCategory.InvalidArgument);
            return Task.FromResult(false);
        }

        protected override Task<bool> PromptToStopRunningInstance(BaseService instance)
        {
            // PowerShell assumes you always want to stop the service if it's running
            return Task.FromResult(true);
        }

        protected override Task<bool> PromptToContinueWithForcedUpgrade()
        {
            // In PowerShell, you passed the -Force parameter to get here in the first place
            return Task.FromResult(true);
        }
    }
}