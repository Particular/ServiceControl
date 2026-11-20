namespace ServiceControl.Management.PowerShell
{
    using System;
    using System.Linq;
    using System.Management.Automation;
    using ServiceControlInstaller.Engine.Instances;

    [Cmdlet(VerbsLifecycle.Start, "ServiceControlMaintenanceMode")]
    public class StartServiceControlMaintenanceMode : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, Position = 0, HelpMessage = "Specify the ServiceControl instance name")]
        public string[] Name { get; set; }

        protected override void BeginProcessing()
        {
            Account.TestIfAdmin();
        }

        protected override void ProcessRecord()
        {
            foreach (var name in Name)
            {
                var instance = InstanceFinder.ServiceControlInstances().FirstOrDefault(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (instance == null)
                {
                    WriteWarning($"No action taken. An instance called {name} was not found");
                    continue;
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
}
