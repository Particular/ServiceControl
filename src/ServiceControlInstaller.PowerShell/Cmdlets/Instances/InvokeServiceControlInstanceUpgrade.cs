// ReSharper disable UnassignedField.Global
// ReSharper disable MemberCanBePrivate.Global
namespace ServiceControlInstaller.PowerShell
{
    using System;
    using System.IO;
    using System.Management.Automation;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Unattended;

    [Cmdlet(VerbsLifecycle.Invoke, "ServiceControlInstanceUpgrade")]
    public class InvokeServiceControlInstanceUpgrade : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, Position = 0, HelpMessage = "Specify the name of the ServiceControl Instance to update")]
        public string[] Name;

        [ValidateNotNullOrEmpty]
        [Parameter(ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, Position = 1, HelpMessage = "Specify if error messages are forwarded to the queue specified by ErrorLogQueue. This setting if appsetting is not set, this occurs when upgrading versions 1.11.1 and below")]
        public bool ForwardErrorMessages { get; set; }

        protected override void BeginProcessing()
        {
            Account.TestIfAdmin();
        }

        protected override void ProcessRecord()
        {
            var logger = new PSLogger(Host);

            var zipFolder = Path.GetDirectoryName(MyInvocation.MyCommand.Module.Path);
            var installer = new UnattendInstaller(logger, zipFolder);
            
            foreach (var name in Name)
            {
                var instance = ServiceControlInstance.FindByName(name);
                if (instance == null)
                {
                    WriteWarning(string.Format("No action taken. An instance called {0} was not found", name));
                    break;
                }

                if (!instance.AppSettingExists("ServiceControl/ForwardErrorMessages"))
                {
                    ThrowTerminatingError(new ErrorRecord(new Exception(string.Format("Upgrade of {0} aborted. ForwardErrorMessages parameter must be set to true or false", instance.Name)), "UpgradeFailure", ErrorCategory.InvalidArgument, null));
                }

                if (!installer.Upgrade(instance))
                {
                    ThrowTerminatingError(new ErrorRecord(new Exception(string.Format("Upgrade of {0} failed",  instance.Name)), "UpgradeFailure", ErrorCategory.InvalidResult, null));
                }
            }
        }
    }
}