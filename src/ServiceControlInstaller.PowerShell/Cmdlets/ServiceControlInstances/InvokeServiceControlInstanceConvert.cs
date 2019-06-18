namespace ServiceControlInstaller.PowerShell
{
    using System;
    using System.IO;
    using System.Management.Automation;
    using Engine.Configuration.ServiceControl;
    using Engine.Instances;
    using Engine.Unattended;

    [Cmdlet(VerbsLifecycle.Invoke, "ServiceControlInstanceConvert")]
    public class InvokeServiceControlInstanceConvert : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, HelpMessage = "Specify the name of the ServiceControl Instance to convert")]
        public string Name;

        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, HelpMessage = "The name of the ServiceControl instance to connect to")]
        public string ServiceControlAddress { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Service Account Password (if required)")]
        public string ServiceAccountPassword { get; set; }

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

            var options = new UnattendServiceControlToAuditInstanceConverter.Options
            {
                AddressOfMainInstance = ServiceControlAddress,
                ServiceAccountPassword = ServiceAccountPassword
            };

            var logger = new PSLogger(Host);
            var zipFolder = Path.GetDirectoryName(MyInvocation.MyCommand.Module.Path);
            var converter = new UnattendServiceControlToAuditInstanceConverter(logger, zipFolder);

            var result = converter.Convert(instance, options);

            WriteObject(result.Succeeded);

            if(!result.Succeeded)
            {
                var errorMessage = GetErrorMessage(instance.Name, result);

                ThrowTerminatingError(new ErrorRecord(new Exception(errorMessage), "UpgradeFailure", ErrorCategory.InvalidResult, null));
            }
        }

        static string GetErrorMessage(string instanceName, UnattendServiceControlToAuditInstanceConverter.Result result)
        {
            if (result.RequiredUpgradeAction.HasValue)
            {
                switch (result.RequiredUpgradeAction.Value)
                {
                    case RequiredUpgradeAction.Upgrade:
                        return $"Conversion of {instanceName} aborted. {result.FailureReason}. See Invoke-ServiceControlInstanceUpgrade.";
                    case RequiredUpgradeAction.SplitOutAudit:
                        return $"Conversion of {instanceName} aborted. {result.FailureReason}. See Invoke-ServiceControlInstanceSplit.";
                }
            }

            return $"Conversion of {instanceName} aborted. {result.FailureReason}.";
        }
    }
}