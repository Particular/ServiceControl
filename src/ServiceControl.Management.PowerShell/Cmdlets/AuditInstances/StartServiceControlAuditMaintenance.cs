namespace ServiceControl.Management.PowerShell
{
    using System;
    using System.Linq;
    using System.Management.Automation;
    using ServiceControlInstaller.Engine.Instances;

    [Cmdlet(VerbsLifecycle.Start, "ServiceControlAuditMaintenance")]
    public class StartServiceControlAuditMaintenance : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, Position = 0, HelpMessage = "Specify the ServiceControl Audit instance name")]
        public string Name { get; set; }

        protected override void BeginProcessing()
        {
            Account.TestIfAdmin();
        }

        protected override void ProcessRecord()
        {
            var instance = InstanceFinder.ServiceControlAuditInstances().FirstOrDefault(i => i.Name.Equals(Name, StringComparison.OrdinalIgnoreCase));
            if (instance == null)
            {
                WriteWarning($"No action taken. An instance called {Name} was not found");
                return;
            }

            if (!instance.TryStopService())
            {
                ThrowTerminatingError(new ErrorRecord(new Exception($"Failed to stop {instance.Name}"), "StopFailure", ErrorCategory.InvalidResult, null));
            }

            instance.EnableMaintenanceMode();

            if (!instance.TryStartService())
            {
                ThrowTerminatingError(new ErrorRecord(new Exception($"Failed to start {instance.Name}"), "StartFailure", ErrorCategory.InvalidResult, null));
            }
        }
    }
}
