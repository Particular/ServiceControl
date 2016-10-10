// ReSharper disable UnassignedField.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace ServiceControlInstaller.PowerShell
{
    using System;
    using System.IO;
    using System.Management.Automation;
    using ServiceControlInstaller.Engine.Configuration;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Unattended;

    [Cmdlet(VerbsLifecycle.Invoke, "ServiceControlInstanceUpgrade", DefaultParameterSetName = "Manual")]
    public class InvokeServiceControlInstanceUpgrade : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, Position = 0, HelpMessage = "Specify the name of the ServiceControl Instance to update")]
        public string Name { get; set; }

        [Parameter(ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, Position = 1, HelpMessage = "Specify if error messages are forwarded to the queue specified by ErrorLogQueue.", ParameterSetName = "Manual")]
        public bool? ForwardErrorMessages { get; set; }

        [Parameter(HelpMessage = "Specify the timespan to keep Audit Data", ParameterSetName = "Manual")]
        [ValidateTimeSpanRange(MinimumHours = 1, MaximumHours = 8760)] //1 hour to 365 days
        public TimeSpan? AuditRetentionPeriod { get; set; }

        [Parameter(HelpMessage = "Specify the timespan to keep Error Data", ParameterSetName = "Manual")]
        [ValidateTimeSpanRange(MinimumHours = 240, MaximumHours = 1080)] //10 to 45 days
        public TimeSpan? ErrorRetentionPeriod { get; set; }

        [Parameter(HelpMessage = "Specify the BodyStorage Path", ParameterSetName = "Manual")]
        public string BodyStoragePath { get; set; }

        [Parameter(HelpMessage = "Specify the IngestionCache Path", ParameterSetName = "Manual")]
        public string IngestionCachePath { get; set; }

        [Parameter(HelpMessage = "Backup the DB on Upgrade")]
        public string BackupPath { get; set; }

        [Parameter(HelpMessage = "Use defaults for AuditRetentionPeriod, ErrorRetentionPeriod, BodyStoragePath, IngestionCachePath and ForwardErrorMessages if required.  Existing values in config will not be overwritten", ParameterSetName = "Automatic")]
        public SwitchParameter Auto { get; set; }

        [Parameter(HelpMessage = "If the service was running prior to upgrade the default behavior is to restart it after upgrade. This switch suppresses the service restart")]
        public SwitchParameter SuppressRestart { get; set; }
        
        protected override void BeginProcessing()
        {
            Account.TestIfAdmin();
        }

        protected override void ProcessRecord()
        {
            var logger = new PSLogger(Host);
            var installer = new UnattendInstaller(logger, Path.GetDirectoryName(MyInvocation.MyCommand.Module.Path));
            var instance = ServiceControlInstance.FindByName(Name);
            if (instance == null)
            {
                ThrowTerminatingError(new ErrorRecord(new Exception($"No action taken. An instance called {Name} was not found"), "UpgradeFailure", ErrorCategory.InvalidArgument, null));
                return;
            }

            var options = Auto.ToBool() ? AutoOptions(instance) : ManualOptions(instance);
            options.SuppressRestart = SuppressRestart.ToBool();
            
            if (!string.IsNullOrWhiteSpace(BackupPath))
            {
                options.BackupRavenDbBeforeUpgrade = true;
                options.BackupPath = BackupPath;
            }

            if (!installer.Upgrade(instance, options))
            {
                ThrowTerminatingError(new ErrorRecord(new Exception($"Upgrade of {instance.Name} failed"), "UpgradeFailure", ErrorCategory.InvalidResult, null));
            }
        }

        private InstanceUpgradeOptions AutoOptions(ServiceControlInstance instance)
        {
            var options = new InstanceUpgradeOptions();

            if (!instance.AppSettingExists(SettingsList.ForwardErrorMessages.Name))
            {
                options.OverrideEnableErrorForwarding = false;
            }

            if (!instance.AppSettingExists(SettingsList.ErrorRetentionPeriod.Name))
            {
                options.ErrorRetentionPeriod = TimeSpan.FromDays(10);
            }

            if (!instance.AppSettingExists(SettingsList.AuditRetentionPeriod.Name))
            {
                options.AuditRetentionPeriod = TimeSpan.FromDays(30);
            }

            if (!instance.AppSettingExists(SettingsList.BodyStoragePath.Name))
            {
                try
                {
                    var parent = Directory.GetParent(instance.DBPath);
                    options.BodyStoragePath = Path.Combine(parent.FullName, "BodyStorage");
                }
                catch (Exception ex)
                {
                    ThrowTerminatingError(new ErrorRecord(new Exception("Failed to determine BodyStorage path", ex), "UpgradeFailure", ErrorCategory.NotSpecified, instance));
                }
            }

            if (!instance.AppSettingExists(SettingsList.IngestionCachePath.Name))
            {
                try
                {
                    var parent = Directory.GetParent(instance.DBPath);
                    options.IngestionCachePath = Path.Combine(parent.FullName, "IngestionCache");
                }
                catch (Exception ex)
                {
                    ThrowTerminatingError(new ErrorRecord(new Exception("Failed to determine BodyStorage path", ex), "UpgradeFailure", ErrorCategory.NotSpecified, instance));
                }
            }
            return options;
        }

        private InstanceUpgradeOptions ManualOptions(ServiceControlInstance instance)
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
                ThrowMissingParameterException(SettingsList.ForwardAuditMessages.Name, nameof(ForwardErrorMessages));
            }

            if (!options.ErrorRetentionPeriod.HasValue & !instance.AppSettingExists(SettingsList.ErrorRetentionPeriod.Name))
            {
                ThrowMissingParameterException( SettingsList.ErrorRetentionPeriod.Name, nameof(ErrorRetentionPeriod));
            }

            if (!options.AuditRetentionPeriod.HasValue & !instance.AppSettingExists(SettingsList.AuditRetentionPeriod.Name))
            {
                ThrowMissingParameterException(SettingsList.AuditRetentionPeriod.Name, nameof(AuditRetentionPeriod));
            }
            
            if (string.IsNullOrWhiteSpace(options.BodyStoragePath) & !instance.AppSettingExists(SettingsList.BodyStoragePath.Name))
            {
                ThrowMissingParameterException(SettingsList.BodyStoragePath.Name, nameof(BodyStoragePath));
            }

            if (string.IsNullOrWhiteSpace(options.IngestionCachePath) & !instance.AppSettingExists(SettingsList.IngestionCachePath.Name))
            {
                ThrowMissingParameterException(SettingsList.IngestionCachePath.Name, nameof(BodyStoragePath));
            }

            return options;
        }

        private void ThrowMissingParameterException(string appsettings, string paramname)
        {
            ThrowTerminatingError(new ErrorRecord(new Exception($"Upgrade aborted. {paramname} parameter must be set because the configuration file has no setting for {appsettings}."), "UpgradeFailure", ErrorCategory.InvalidArgument, null));
        }
    }
}