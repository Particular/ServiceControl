namespace ServiceControlInstaller.PowerShell
{
    using System;
    using System.Management.Automation;
    using Engine.Instances;
    using Engine.Unattended;
    using ServiceControlInstaller.Engine.Validation;

    [Cmdlet(VerbsLifecycle.Invoke, "MonitoringInstanceUpgrade")]
    public class InvokeMonitoringUpgrade : PSCmdlet
    {
        [Parameter(Mandatory = false, HelpMessage = "Do not automatically create new queues")]
        public SwitchParameter SkipQueueCreation { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Acknowledge transport prerequisites have been met.")]
        public SwitchParameter AcknowledgeTransportMinimumRequirements { get; set; }

        protected override void BeginProcessing()
        {
            AppDomain.CurrentDomain.AssemblyResolve += BindingRedirectAssemblyLoader.CurrentDomain_BindingRedirect;

            Account.TestIfAdmin();
        }

        protected override void ProcessRecord()
        {
            var logger = new PSLogger(Host);

            var zipFolder = ZipPath.Get(this);
            var installer = new UnattendMonitoringInstaller(logger, zipFolder);

            foreach (var name in Name)
            {
                var instance = InstanceFinder.FindMonitoringInstance(name);
                if (instance == null)
                {
                    WriteWarning($"No action taken. An instance called {name} was not found");
                    break;
                }

                if ((instance.TransportPackage.Name == TransportNames.RabbitMQConventionalRoutingTopologyDeprecated ||
                     instance.TransportPackage.Name == TransportNames.RabbitMQDirectRoutingTopologyDeprecated) &&
                    !AcknowledgeTransportMinimumRequirements.ToBool())
                {
                    throw new EngineValidationException($"ServiceControl version {installer.ZipInfo.Version} requires RabbitMQ broker version 3.10.0 or higher. Also, the stream_queue and quorum_queue feature flags must be enabled on the broker. Use -AcknowledgeTransportMinimumRequirements if you are sure your broker meets these requirements.");
                }

                instance.SkipQueueCreation = SkipQueueCreation;

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