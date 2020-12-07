namespace ServiceControlInstaller.PowerShell
{
    using System;
    using System.IO;
    using System.Management.Automation;
    using Engine.Instances;
    using Engine.Unattended;

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

            var zipFolder = Path.GetDirectoryName(MyInvocation.MyCommand.Module.Path);
            var installer = new UnattendServiceControlInstaller(logger, zipFolder);

            var instance = InstanceFinder.FindInstanceByName<ServiceControlInstance>(Name);
            if (instance == null)
            {
                WriteWarning($"No action taken. An instance called {Name} was not found");
                return;
            }

            WriteObject(installer.AddRemoteInstance(instance, RemoteInstanceAddress, logger));
        }
    }
}