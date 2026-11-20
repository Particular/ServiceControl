namespace ServiceControl.Management.PowerShell
{
    using System;
    using System.Management.Automation;
    using ServiceControlInstaller.Engine.Instances;

    public abstract class MaintenanceModeCmdletBase : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, Position = 0, HelpMessage = "Specify the instance name")]
        public string[] Name { get; set; }

        protected abstract bool StartInMaintenanceMode { get; }

        protected abstract ServiceControlBaseService FindInstance(string name);

        protected override void BeginProcessing()
        {
            Account.TestIfAdmin();
        }

        protected override void ProcessRecord()
        {
            foreach (var name in Name)
            {
                var instance = FindInstance(name);
                if (instance == null)
                {
                    WriteWarning($"No action taken. An instance called {name} was not found");
                    continue;
                }

                if (!instance.TryStopService())
                {
                    ThrowTerminatingError(new ErrorRecord(new Exception($"Failed to stop {instance.Name}"), "StopFailure", ErrorCategory.InvalidResult, null));
                }

                if (!StartInMaintenanceMode)
                {
                    instance.DisableMaintenanceMode();
                    continue;
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
