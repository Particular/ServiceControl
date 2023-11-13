namespace ServiceControl.Management.PowerShell
{
    using System;
    using System.Linq;
    using System.Management.Automation;
    using ServiceControl.Engine.Extensions;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Unattended;
    using ServiceControlInstaller.Engine.Validation;
    using Validation;

    [Cmdlet(VerbsLifecycle.Invoke, "ServiceControlInstanceUpgrade")]
    public class InvokeServiceControlInstanceUpgrade : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, Position = 0, HelpMessage = "Specify the name of the ServiceControl Instance to update")]
        public string Name;

        [Parameter(Mandatory = false, HelpMessage = "Do not automatically create new queues")]
        public SwitchParameter SkipQueueCreation { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Disable full text search on error messages.")]
        public SwitchParameter DisableFullTextSearchOnBodies { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Perform upgrade even if current instance is using an obsolete incompatibiel RavenDB3.5 storage engine resulting in data loss and reuse the specified log, db, and install paths even if they are not empty")]
        public SwitchParameter Force { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Acknowledge mandatory requirements have been met.")]
        public string[] Acknowledgements { get; set; }

        protected override void BeginProcessing()
        {
            Account.TestIfAdmin();
        }

        protected override void ProcessRecord()
        {
            var logger = new PSLogger(Host);

            var installer = new UnattendServiceControlInstaller(logger);

            var instance = InstanceFinder.FindInstanceByName<ServiceControlInstance>(Name);
            if (instance == null)
            {
                WriteWarning($"No action taken. An instance called {Name} was not found");
                return;
            }

            PerformUpgrade(instance, installer);
        }

        void PerformUpgrade(ServiceControlInstance instance, UnattendServiceControlInstaller installer)
        {
            var options = new ServiceControlUpgradeOptions
            {
                SkipQueueCreation = SkipQueueCreation,
                DisableFullTextSearchOnBodies = DisableFullTextSearchOnBodies,
                Force = Force.IsPresent
            };

            if (DotnetVersionValidator.FrameworkRequirementsAreMissing(needsRavenDB: true, out var missingMessage))
            {
                ThrowTerminatingError(new ErrorRecord(new Exception(missingMessage), "Missing Prerequisites", ErrorCategory.NotInstalled, null));
            }

            if (instance.TransportPackage.IsOldRabbitMQTransport() &&
               (Acknowledgements == null || !Acknowledgements.Any(ack => ack.Equals(AcknowledgementValues.RabbitMQBrokerVersion310, StringComparison.OrdinalIgnoreCase))))
            {
                ThrowTerminatingError(new ErrorRecord(new Exception($"ServiceControl version {installer.ZipInfo.Version} requires RabbitMQ broker version 3.10.0 or higher. Also, the stream_queue and quorum_queue feature flags must be enabled on the broker. Use -Acknowledgements {AcknowledgementValues.RabbitMQBrokerVersion310} if you are sure your broker meets these requirements."), "Install Error", ErrorCategory.InvalidArgument, null));
            }

            if (!installer.Upgrade(instance, options))
            {
                ThrowTerminatingError(new ErrorRecord(new Exception($"Upgrade of {instance.Name} failed"), "UpgradeFailure", ErrorCategory.InvalidResult, null));
            }
        }
    }
}
