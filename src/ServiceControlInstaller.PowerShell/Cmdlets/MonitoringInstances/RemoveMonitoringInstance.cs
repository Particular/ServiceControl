namespace ServiceControlInstaller.PowerShell
{
    using System;
    using System.Management.Automation;
    using Engine.Instances;
    using Engine.Unattended;

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
            AppDomain.CurrentDomain.AssemblyResolve += BindingRedirectAssemblyLoader.CurrentDomain_BindingRedirect;

            Account.TestIfAdmin();
        }

        protected override void ProcessRecord()
        {
            var logger = new PSLogger(Host);
            var zipfolder = ZipPath.Get(this);
            var installer = new UnattendMonitoringInstaller(logger, zipfolder);

            foreach (var name in Name)
            {
                var instance = InstanceFinder.FindMonitoringInstance(name);
                if (instance == null)
                {
                    WriteWarning($"No action taken. An instance called {name} was not found");
                    break;
                }

                WriteObject(installer.Delete(instance.Name, RemoveLogs.ToBool()));
            }
        }
    }
}