namespace ServiceControl.Management.PowerShell
{
    using System;
    using System.Management.Automation;
    using ServiceControl.Management.PowerShell.Validation;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Unattended;

    [Cmdlet(VerbsCommon.Add, "ServiceControlRemote")]
    public class AddServiceControlRemote : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, Position = 0, HelpMessage = "The name of the ServiceControl instance to add the remote to")]
        public string Name { get; set; }

        [ValidateNotNull]
        [ValidateUrl]
        [ValidateCount(1, int.MaxValue)]
        [Parameter(Mandatory = true, Position = 1, ValueFromRemainingArguments = true, HelpMessage = "The api url of the remote instance")]
        public string[] RemoteInstanceAddress { get; set; }

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

            var checks = new PowerShellCommandChecks(this);
            if (!checks.CanEditInstance(instance).GetAwaiter().GetResult())
            {
                return;
            }

            var success = installer.AddRemoteInstance(instance, RemoteInstanceAddress, logger);

            if (!success)
            {
                ThrowTerminatingError(new ErrorRecord(new Exception($"Adding of ServiceControl remote failed"), "AddRemoteFailure", ErrorCategory.InvalidResult, null));
            }
        }
    }
}