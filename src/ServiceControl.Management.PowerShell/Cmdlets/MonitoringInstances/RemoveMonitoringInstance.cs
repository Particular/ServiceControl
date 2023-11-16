namespace ServiceControl.Management.PowerShell
{
    using System.Management.Automation;
    using ServiceControl.Management.PowerShell.Validation;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Unattended;

    [Cmdlet(VerbsCommon.Remove, "MonitoringInstance")]
    public class RemoveMonitoringInstance : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, Position = 0, HelpMessage = "Specify the ServiceControl instance name to remove")]
        public string[] Name { get; set; }

        [Parameter(HelpMessage = "Remove the Logs directory")]
        public SwitchParameter RemoveLogs { get; set; }

        protected override void BeginProcessing()
        {
            Account.TestIfAdmin();
        }

        protected override void ProcessRecord()
        {
            var logger = new PSLogger(Host);
            var installer = new UnattendMonitoringInstaller(logger);

            foreach (var name in Name)
            {
                var instance = InstanceFinder.FindMonitoringInstance(name);
                if (instance == null)
                {
                    WriteWarning($"No action taken. An instance called {name} was not found");
                    break;
                }

                var checks = new PowerShellCommandChecks(this);
                if (!checks.CanDeleteInstance(instance).GetAwaiter().GetResult())
                {
                    return;
                }

                WriteObject(installer.Delete(instance.Name, RemoveLogs.ToBool()));
            }
        }
    }
}