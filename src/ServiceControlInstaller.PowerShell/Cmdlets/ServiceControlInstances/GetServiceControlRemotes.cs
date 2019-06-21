namespace ServiceControlInstaller.PowerShell
{
    using System.Linq;
    using System.Management.Automation;
    using Engine.Instances;

    [Cmdlet(VerbsCommon.Get, "ServiceControlRemotes")]
    public class GetServiceControlRemotes : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, Position = 0, HelpMessage = "The name of the ServiceControl instance to remove the remote from")]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            var instance = InstanceFinder.FindInstanceByName<ServiceControlInstance>(Name);
            if (instance == null)
            {
                WriteWarning($"No action taken. An instance called {Name} was not found");
                return;
            }

            WriteObject(instance.RemoteInstances.Select(PsServiceControlRemote.FromRemote), true);
        }
    }
}