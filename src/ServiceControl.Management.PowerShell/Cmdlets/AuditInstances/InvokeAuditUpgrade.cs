namespace ServiceControl.Management.PowerShell
{
    using System;
    using System.Linq;
    using System.Management.Automation;
    using ServiceControl.Engine.Extensions;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Unattended;
    using Validation;

    [Cmdlet(VerbsLifecycle.Invoke, "ServiceControlAuditInstanceUpgrade")]
    public class InvokeServiceControlAuditInstanceUpgrade : PSCmdlet
    {
        [Parameter(Mandatory = false, HelpMessage = "Do not automatically create new queues")]
        public SwitchParameter SkipQueueCreation { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Disable full text search on audit messages.")]
        public SwitchParameter DisableFullTextSearchOnBodies { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Acknowledge mandatory requirements have been met.")]
        public string[] Acknowledgements { get; set; }

        protected override void BeginProcessing()
        {
            AppDomain.CurrentDomain.AssemblyResolve += BindingRedirectAssemblyLoader.CurrentDomain_BindingRedirect;

            Account.TestIfAdmin();
        }

        protected override void ProcessRecord()
        {
            var logger = new PSLogger(Host);

            var zipFolder = ZipPath.Get(this);
            var installer = new UnattendAuditInstaller(logger, zipFolder);

            foreach (var name in Name)
            {
                if (InstanceFinder.FindServiceControlInstance(name) is not ServiceControlAuditInstance instance)
                {
                    WriteWarning($"No action taken. An audit instance called {name} was not found");
                    break;
                }

                instance.SkipQueueCreation = SkipQueueCreation;
                if (DisableFullTextSearchOnBodies)
                {
                    instance.EnableFullTextSearchOnBodies = false;
                }

                if (instance.TransportPackage.IsOldRabbitMQTransport() &&
                   (Acknowledgements == null || !Acknowledgements.Any(ack => ack.Equals(AcknowledgementValues.RabbitMQBrokerVersion310, StringComparison.OrdinalIgnoreCase))))
                {
                    ThrowTerminatingError(new ErrorRecord(new Exception($"ServiceControl version {installer.ZipInfo.Version} requires RabbitMQ broker version 3.10.0 or higher. Also, the stream_queue and quorum_queue feature flags must be enabled on the broker. Use -Acknowledgements {AcknowledgementValues.RabbitMQBrokerVersion310} if you are sure your broker meets these requirements."), "Install Error", ErrorCategory.InvalidArgument, null));
                }

                if (!installer.Upgrade(instance))
                {
                    ThrowTerminatingError(new ErrorRecord(new Exception($"Upgrade of {instance.Name} failed"), "UpgradeFailure", ErrorCategory.InvalidResult, null));
                }
            }
        }

        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, Position = 0, HelpMessage = "Specify the name of the ServiceControl Instance to update")]
        public string[] Name;
    }
}