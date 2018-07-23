// ReSharper disable UnassignedField.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace ServiceControlInstaller.PowerShell
{
    using System.IO;
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
            Account.TestIfAdmin();
        }

        protected override void ProcessRecord()
        {
            var logger = new PSLogger(Host);
            var zipfolder = Path.GetDirectoryName(MyInvocation.MyCommand.Module.Path);
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