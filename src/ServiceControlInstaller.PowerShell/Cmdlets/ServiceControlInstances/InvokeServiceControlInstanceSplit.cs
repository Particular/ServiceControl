namespace ServiceControlInstaller.PowerShell
{
    using System;
    using System.IO;
    using System.Management.Automation;
    using Engine.Configuration.ServiceControl;
    using Engine.Instances;
    using Engine.Unattended;
    using Engine.Validation;
    using PathInfo = Engine.Validation.PathInfo;

    [Cmdlet(VerbsLifecycle.Invoke, "ServiceControlInstanceSplit")]
    public class InvokeServiceControlInstanceSplit : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, HelpMessage = "Specify the name of the ServiceControl Instance to split")]
        public string Name;

        [Parameter(Mandatory = true, HelpMessage = "Specify the directory to use for the new ServiceControl Audit Instance")]
        [ValidateNotNullOrEmpty]
        [ValidatePath]
        public string InstallPath { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify the directory that will contain the RavenDB database for the new ServiceControl Audit Instance")]
        [ValidateNotNullOrEmpty]
        [ValidatePath]
        public string DBPath { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify the directory to use for the new ServiceControl Audit Logs")]
        [ValidateNotNullOrEmpty]
        [ValidatePath]
        public string LogPath { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify the port number for the new ServiceControl Audit API to listen on")]
        [ValidateRange(1, 49151)]
        public int Port { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify the database maintenance port number for the new ServiceControl Audit instance to listen on")]
        [ValidateRange(1, 49151)]
        public int DatabaseMaintenancePort { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Service Account Password (if required)")]
        public string ServiceAccountPassword { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Reuse the specified log, db, and install paths even if they are not empty")]
        public SwitchParameter Force { get; set; }


        protected override void BeginProcessing()
        {
            Account.TestIfAdmin();
        }

        protected override void ProcessRecord()
        {
            var instance = InstanceFinder.FindInstanceByName<ServiceControlInstance>(Name);

            if (instance == null)
            {
                WriteWarning($"No action taken. An instance called {Name} was not found");
                return;
            }

            var options = new UnattendServiceControlSplitter.Options
            {
                InstallPath = InstallPath,
                DBPath = DBPath,
                LogPath = LogPath,
                Port = Port,
                DatabaseMaintenancePort = DatabaseMaintenancePort,
                ServiceAccountPassword = ServiceAccountPassword
            };

            var logger = new PSLogger(Host);
            var zipFolder = Path.GetDirectoryName(MyInvocation.MyCommand.Module.Path);
            var serviceControlSplitter = new UnattendServiceControlSplitter(logger, zipFolder);

            var result = serviceControlSplitter.Split(instance, options, PromptToProceed);

            WriteObject(result.Succeeded);

            if (!result.Succeeded)
            {
                var errorMessage = GetErrorMessage(instance.Name, result);

                ThrowTerminatingError(new ErrorRecord(new Exception(errorMessage), "UpgradeFailure", ErrorCategory.InvalidResult, null));
            }
        }

        private bool PromptToProceed(PathInfo pathInfo)
        {
            if (!pathInfo.CheckIfEmpty)
            {
                return false;
            }

            if (!Force.ToBool())
            {
                throw new EngineValidationException($"The directory specified for {pathInfo.Name} is not empty.  Use -Force if you are sure you want to use this path");
            }

            WriteWarning($"The directory specified for {pathInfo.Name} is not empty but will be used as -Force was specified");
            return false;
        }

        static string GetErrorMessage(string instanceName, UnattendServiceControlSplitter.Result result)
        {
            if (result.RequiredUpgradeAction == RequiredUpgradeAction.Upgrade)
            {
                return $"Split of {instanceName} aborted. {result.FailureReason}. See Invoke-ServiceControlInstanceUpgrade.";
            }

            return $"Split of {instanceName} aborted. {result.FailureReason}.";
        }
    }
}