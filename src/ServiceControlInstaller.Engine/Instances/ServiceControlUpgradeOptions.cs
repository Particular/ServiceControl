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

        public void ApplyChangesToInstance(ServiceControlBaseService instance)
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

            instance.SkipQueueCreation = SkipQueueCreation;
        }
    }
}