namespace ServiceControl.Management.PowerShell.Validation
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Management.Automation;
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

        protected override Task NotifyForDeprecatedMessageTransport(TransportInfo transport) => throw new NotImplementedException();
        protected override Task NotifyForIncompatibleStorageEngine(IServiceControlBaseInstance baseInstance) => throw new NotImplementedException();
        protected override Task NotifyForIncompatibleUpgradeVersion(UpgradeInfo upgradeInfo) => throw new NotImplementedException();
        protected override Task NotifyForLicenseIssue(string licenseMessage) => throw new NotImplementedException();

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

        protected override Task<bool> PromptToStopRunningInstance(BaseService instance) => throw new NotImplementedException();
    }
}
