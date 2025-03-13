namespace ServiceControlInstaller.Engine.Instances
{
    using System;

    public class ServiceControlUpgradeOptions
    {
        public bool? OverrideEnableErrorForwarding { get; set; }
        public TimeSpan? ErrorRetentionPeriod { get; set; }
        public TimeSpan? AuditRetentionPeriod { get; set; }
        public int? MaintenancePort { get; set; }
        public bool SkipQueueCreation { get; set; }
        public string RemoteUrl { get; set; }
        public bool Force { get; set; }
        public string UpgradedConnectionString { get; set; }

        public void ApplyChangesToInstance(ServiceControlBaseService instance)
        {
            ApplyChangesTo(instance as ServiceControlInstance);
            ApplyChangesTo(instance as ServiceControlAuditInstance);

            instance.ApplyConfigChange();
        }

        void ApplyChangesTo(ServiceControlInstance instance)
        {
            if (instance == null)
            {
                return;
            }

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

            if (!string.IsNullOrWhiteSpace(RemoteUrl))
            {
                instance.AddRemoteInstance(RemoteUrl);
            }

            instance.SkipQueueCreation = SkipQueueCreation;

            if (!string.IsNullOrEmpty(UpgradedConnectionString))
            {
                instance.ConnectionString = UpgradedConnectionString;
            }
        }

        void ApplyChangesTo(ServiceControlAuditInstance instance)
        {
            if (instance == null)
            {
                return;
            }
        }
    }
}