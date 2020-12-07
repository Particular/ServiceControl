namespace ServiceControlInstaller.PowerShell
{
    using System;
    using System.IO;
    using System.Management.Automation;
    using Engine.Instances;
    using Engine.Unattended;

    [Cmdlet(VerbsCommon.Remove, "ServiceControlRemote")]
    public class RemoveServiceControlRemote : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, Position = 0, HelpMessage = "The name of the ServiceControl instance to remove the remote from")]
        public string Name { get; set; }

        [ValidateNotNull]
        [ValidateUrl]
        [ValidateCount(1, int.MaxValue)]
        [Parameter(Mandatory = true, Position = 1, ValueFromRemainingArguments = true, HelpMessage = "The api url of the remote instance to remove")]
        public string[] RemoteInstanceAddress { get; set; }

        protected override void ProcessRecord()
        {
            var logger = new PSLogger(Host);

            var zipFolder = Path.Combine(Path.GetDirectoryName(MyInvocation.MyCommand.Module.Path), "..");
            var installer = new UnattendServiceControlInstaller(logger, zipFolder);

            var instance = InstanceFinder.FindInstanceByName<ServiceControlInstance>(Name);
            if (instance == null)
            {
                WriteWarning($"No action taken. An instance called {Name} was not found");
                return;
            }

            WriteObject(installer.RemoveRemoteInstance(instance, RemoteInstanceAddress, logger));
        }
    }
}