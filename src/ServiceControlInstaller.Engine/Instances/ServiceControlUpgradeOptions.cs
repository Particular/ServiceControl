namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using Configuration.ServiceControl;

    public class ServiceControlUpgradeOptions
    {
        public bool? OverrideEnableErrorForwarding { get; set; }
        public bool? EnableFullTextSearchOnBodies { get; set; }
        public TimeSpan? ErrorRetentionPeriod { get; set; }
        public TimeSpan? AuditRetentionPeriod { get; set; }
        public int? MaintenancePort { get; set; }
        public bool SkipQueueCreation { get; set; }
        public UpgradeInfo UpgradeInfo { get; set; }
        public string RemoteUrl { get; set; }

        public void ApplyChangesToInstance(ServiceControlBaseService instance)
        {
            if (EnableFullTextSearchOnBodies.HasValue)
            {
                instance.EnableFullTextSearchOnBodies = EnableFullTextSearchOnBodies.Value;
            }

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

            if (EnableFullTextSearchOnBodies.HasValue)
            {
                instance.EnableFullTextSearchOnBodies = EnableFullTextSearchOnBodies.Value;
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