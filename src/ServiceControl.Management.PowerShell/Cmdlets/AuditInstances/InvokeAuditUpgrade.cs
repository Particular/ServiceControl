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

    [Cmdlet(VerbsLifecycle.Invoke, "ServiceControlAuditInstanceUpgrade")]
    public class InvokeServiceControlAuditInstanceUpgrade : PSCmdlet
    {
        [Parameter(Mandatory = false, HelpMessage = "Do not automatically create new queues")]
        public SwitchParameter SkipQueueCreation { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Disable full text search on audit messages.")]
        public SwitchParameter DisableFullTextSearchOnBodies { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Acknowledge mandatory requirements have been met.")]
        public string[] Acknowledgements { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Perform upgrade even if the current instance is using obsolete, incompatible RavenDB 3.5 storage engine. Replaces the database with a brand new one, removing all data previously stored.")]
        public SwitchParameter Force { get; set; }

        protected override void BeginProcessing()
        {
            Account.TestIfAdmin();
        }

        protected override void ProcessRecord()
        {
            var logger = new PSLogger(Host);

            var installer = new UnattendAuditInstaller(logger);

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

                var checks = new PowerShellCommandChecks(this, Acknowledgements);
                if (!checks.CanUpgradeInstance(instance, Force.IsPresent).GetAwaiter().GetResult())
                {
                    return;
                }

                if (!installer.Upgrade(instance, Force.IsPresent))
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