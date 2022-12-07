namespace ServiceControl.Management.PowerShell
{
    using System;
    using System.Management.Automation;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Unattended;

    [Cmdlet(VerbsCommon.Remove, "ServiceControlAuditInstance")]
    public class RemoveServiceControlAuditInstance : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, Position = 0, HelpMessage = "Specify the ServiceControl Audit instance name to remove")]
        public string[] Name { get; set; }

        [Parameter(HelpMessage = "Remove the RavenDB database")]
        public SwitchParameter RemoveDB { get; set; }

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
            var installer = new UnattendAuditInstaller(logger, zipfolder);

            foreach (var name in Name)
            {
                var instance = InstanceFinder.FindServiceControlInstance(name);
                if (instance == null)
                {
                    WriteWarning($"No action taken. An instance called {name} was not found");
                    break;
                }

                WriteObject(installer.Delete(instance.Name, RemoveDB.ToBool(), RemoveLogs.ToBool()));
            }
        }
    }
}