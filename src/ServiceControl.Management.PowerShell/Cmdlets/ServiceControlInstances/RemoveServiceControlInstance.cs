namespace ServiceControl.Management.PowerShell
{
    using System;
    using System.Management.Automation;
    using ServiceControl.Management.PowerShell.Validation;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Unattended;

    [Cmdlet(VerbsCommon.Remove, "ServiceControlInstance")]
    public class RemoveServiceControlInstance : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, Position = 0, HelpMessage = "Specify the ServiceControl instance name to remove")]
        public string[] Name { get; set; }

        [Parameter(HelpMessage = "Remove the local database files and data, if the instance's storage type keeps any locally (e.g. RavenDB). Has no effect for storage types backed by an external database (e.g. SQL Server, PostgreSQL) - those are not touched and must be removed manually")]
        public SwitchParameter RemoveDB { get; set; }

        [Parameter(HelpMessage = "Remove the Logs directory")]
        public SwitchParameter RemoveLogs { get; set; }

        protected override void BeginProcessing()
        {
            Account.TestIfAdmin();
        }

        protected override void ProcessRecord()
        {
            var logger = new PSLogger(Host);
            var installer = new UnattendServiceControlInstaller(logger);

            foreach (var name in Name)
            {
                var instance = InstanceFinder.FindServiceControlInstance(name);
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

                var success = installer.Delete(instance.Name, RemoveDB.ToBool(), RemoveLogs.ToBool());

                if (!success)
                {
                    ThrowTerminatingError(new ErrorRecord(new Exception($"Removal of {instance.Name} failed"), "RemovalFailure", ErrorCategory.InvalidResult, null));
                }
            }
        }
    }
}