namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using Configuration.ServiceControl;

    public class ServiceControlUpgradeOptions
    {
        public bool? OverrideEnableErrorForwarding { get; set; }
        public TimeSpan? ErrorRetentionPeriod { get; set; }
        public TimeSpan? AuditRetentionPeriod { get; set; }
        public int? MaintenancePort { get; set; }
        public bool SkipQueueCreation { get; set; }
        public UpgradeInfo UpgradeInfo { get; set; }
        public string RemoteUrl { get; set; }
        public bool InstallNewAuditSidecar { get; set; }

        public void ApplyChangesToInstance(ServiceControlInstance instance)
        {
            if (OverrideEnableErrorForwarding.HasValue)
            {
                instance.ForwardErrorMessages = OverrideEnableErrorForwarding.Value;
            }

            if (ErrorRetentionPeriod.HasValue)
            {
                instance.ErrorRetentionPeriod = ErrorRetentionPeriod.Value;
            }

            if (AuditRetentionPeriod.HasValue)
            {
                instance.AuditRetentionPeriod = AuditRetentionPeriod.Value;
            }

            if (MaintenancePort.HasValue)
            {
                instance.DatabaseMaintenancePort = MaintenancePort;
            }

            if (string.IsNullOrWhiteSpace(RemoteUrl) == false)
            {
                instance.AddRemoteInstance(RemoteUrl);
            }

            instance.SkipQueueCreation = SkipQueueCreation;

            instance.ApplyConfigChange();
        }
    }
}