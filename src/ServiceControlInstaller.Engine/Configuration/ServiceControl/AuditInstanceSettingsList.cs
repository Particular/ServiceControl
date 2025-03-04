namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using NuGet.Versioning;

    public static class AuditInstanceSettingsList
    {
        public static readonly SettingInfo InternalQueueName = new()
        {
            Name = "ServiceControl.Audit/InternalQueueName",
            RemovedFrom = new(5, 5, 0)
        };

        public static readonly SettingInfo InstanceName = new()
        {
            Name = "ServiceControl.Audit/InstanceName",
            SupportedFrom = new SemanticVersion(5, 5, 0)
        };

        public static readonly SettingInfo Port = new() { Name = "ServiceControl.Audit/Port" };
        public static readonly SettingInfo DatabaseMaintenancePort = new() { Name = "ServiceControl.Audit/DatabaseMaintenancePort" };
        public static readonly SettingInfo HostName = new() { Name = "ServiceControl.Audit/HostName" };
        public static readonly SettingInfo LogPath = new() { Name = "ServiceControl.Audit/LogPath" };
        public static readonly SettingInfo DBPath = new() { Name = "ServiceControl.Audit/DBPath" };
        public static readonly SettingInfo ForwardAuditMessages = new() { Name = "ServiceControl.Audit/ForwardAuditMessages" };
        public static readonly SettingInfo TransportType = new() { Name = "ServiceControl.Audit/TransportType" };
        public static readonly SettingInfo PersistenceType = new() { Name = "ServiceControl.Audit/PersistenceType" };
        public static readonly SettingInfo AuditQueue = new() { Name = "ServiceBus/AuditQueue" };
        public static readonly SettingInfo AuditLogQueue = new() { Name = "ServiceBus/AuditLogQueue" };
        public static readonly SettingInfo AuditRetentionPeriod = new() { Name = "ServiceControl.Audit/AuditRetentionPeriod" };
        public static readonly SettingInfo MaintenanceMode = new() { Name = "ServiceControl.Audit/MaintenanceMode" };
        public static readonly SettingInfo ServiceControlQueueAddress = new() { Name = "ServiceControl.Audit/ServiceControlQueueAddress" };

        public static readonly SettingInfo EnableFullTextSearchOnBodies = new()
        {
            Name = "ServiceControl.Audit/EnableFullTextSearchOnBodies",
            SupportedFrom = new SemanticVersion(4, 17, 0)
        };

        public static readonly SettingInfo ShutdownTimeout = new()
        {
            Name = "ServiceControl.Audit/ShutdownTimeout",
            SupportedFrom = new SemanticVersion(6, 4, 1)
        };
    }
}