// ReSharper disable UnassignedField.Global
// ReSharper disable MemberCanBePrivate.Global
namespace ServiceControlInstaller.PowerShell
{
    using System;
    using System.IO;
    using System.Management.Automation;
    using ServiceControlInstaller.Engine.Configuration;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Unattended;

    [Cmdlet(VerbsLifecycle.Invoke, "ServiceControlInstanceUpgrade")]
    public class InvokeServiceControlInstanceUpgrade : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, Position = 0, HelpMessage = "Specify the name of the ServiceControl Instance to update")]
        public string[] Name;

        [Parameter(ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, Position = 1, HelpMessage = "Specify if error messages are forwarded to the queue specified by ErrorLogQueue. This setting if appsetting is not set, this occurs when upgrading versions 1.11.1 and below")]
        public bool? ForwardErrorMessages;

        [Parameter(HelpMessage = "Specify the timespan to keep Audit Data")]
        [ValidateTimeSpanRange(MinimumHours = 1, MaximumHours = 8760)] //1 hour to 365 days
        public TimeSpan? AuditRetentionPeriod { get; set; }

        [Parameter(HelpMessage = "Specify the timespan to keep Error Data")]
        [ValidateTimeSpanRange(MinimumHours = 240, MaximumHours = 1080)] //10 to 45 days
        public TimeSpan? ErrorRetentionPeriod { get; set; }

        [Parameter(HelpMessage = "Specify the BodyStorage Path ")]
        public string BodyStoragePath { get; set; }

        [Parameter(HelpMessage = "Specify the IngestionCache Path")]
        public string IngestionCachePath { get; set; }

        [Parameter(HelpMessage = "Backup the DB on Upgrade")]
        public string BackupPath{ get; set; }
        
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
                var options = new InstanceUpgradeOptions
                {
                    AuditRetentionPeriod = AuditRetentionPeriod,
                    ErrorRetentionPeriod = ErrorRetentionPeriod,
                    OverrideEnableErrorForwarding = ForwardErrorMessages,
                    BodyStoragePath =  BodyStoragePath,
                    IngestionCachePath = IngestionCachePath,
                    BackupPath = BackupPath
                };
                var instance = ServiceControlInstance.FindByName(name);
                if (instance == null)
                {
                    WriteWarning($"No action taken. An instance called {name} was not found");
                    break;
                }

                options.OverrideEnableErrorForwarding = ForwardErrorMessages;
                
                // Migrate Value
                if (!options.AuditRetentionPeriod.HasValue)
                {
                    if (instance.AppSettingExists(SettingsList.HoursToKeepMessagesBeforeExpiring.Name))
                    {
                        var i = instance.ReadAppSetting(SettingsList.HoursToKeepMessagesBeforeExpiring.Name, -1);
                        if (i != -1)
                        {
                            options.AuditRetentionPeriod = TimeSpan.FromHours(i);
                        }
                    }
                }
                
                if (!options.OverrideEnableErrorForwarding.HasValue & !instance.AppSettingExists(SettingsList.ForwardErrorMessages.Name))
                {
                    ThrowTerminatingError(new ErrorRecord(new Exception($"Upgrade of {instance.Name} aborted. ForwardErrorMessages parameter must be set to true or false because the configuration file has no setting for ForwardErrorMessages."), "UpgradeFailure", ErrorCategory.InvalidArgument, null));
                }

                if (!options.ErrorRetentionPeriod.HasValue & !instance.AppSettingExists(SettingsList.ErrorRetentionPeriod.Name))
                {
                    ThrowTerminatingError(new ErrorRecord(new Exception($"Upgrade of {instance.Name} aborted. ErrorRetentionPeriod parameter must be set to timespan because the configuration file has no setting for ErrorRetentionPeriod. "), "UpgradeFailure", ErrorCategory.InvalidArgument, null));
                }
                
                if (!options.AuditRetentionPeriod.HasValue & !instance.AppSettingExists(SettingsList.AuditRetentionPeriod.Name))
                {
                    ThrowTerminatingError(new ErrorRecord(new Exception($"Upgrade of {instance.Name} aborted. AuditRetentionPeriod parameter must be set to timespan because the configuration file has no setting for AuditRetentionPeriod. "), "UpgradeFailure", ErrorCategory.InvalidArgument, null));
                }

                if (!options.OverrideEnableErrorForwarding.HasValue & !instance.AppSettingExists(SettingsList.ForwardErrorMessages.Name))
                {
                    ThrowTerminatingError(new ErrorRecord(new Exception($"Upgrade of {instance.Name} aborted.{name} parameter must be set to true or false because the configuration file has no setting for ForwardErrorMessages."), "UpgradeFailure", ErrorCategory.InvalidArgument, null));
                }

                if (string.IsNullOrWhiteSpace(options.BodyStoragePath) & !instance.AppSettingExists(SettingsList.BodyStoragePath.Name))
                {
                    ThrowTerminatingError(new ErrorRecord(new Exception($"Upgrade of {instance.Name} aborted. BodyStoragePath parameter must be set because the configuration file has no setting for this."), "UpgradeFailure", ErrorCategory.InvalidArgument, null));
                }

                if (string.IsNullOrWhiteSpace(options.IngestionCachePath) & !instance.AppSettingExists(SettingsList.IngestionCachePath.Name))
                {
                    ThrowTerminatingError(new ErrorRecord(new Exception($"Upgrade of {instance.Name} aborted. IngestionCachePath parameter must be set because the configuration file has no setting for this."), "UpgradeFailure", ErrorCategory.InvalidArgument, null));
                }

                if (string.IsNullOrWhiteSpace(options.BodyStoragePath) & !instance.AppSettingExists(SettingsList.BodyStoragePath.Name))
                {
                    ThrowTerminatingError(new ErrorRecord(new Exception($"Upgrade of {instance.Name} aborted. BodyStoragePath parameter must be set because the configuration file has no setting for this."), "UpgradeFailure", ErrorCategory.InvalidArgument, null));
                }

                if (!string.IsNullOrWhiteSpace(options.BackupPath))
                {
                    options.BackupRavenDbBeforeUpgrade = true;
                }

                logger.Info("Attempting upgrade...");
                if (!installer.Upgrade(instance, options))
                {
                    ThrowTerminatingError(new ErrorRecord(new Exception($"Upgrade of {instance.Name} failed"), "UpgradeFailure", ErrorCategory.InvalidResult, null));
                }
            }
        }
    }
}