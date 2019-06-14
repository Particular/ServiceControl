namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    public static class AuditInstanceSettingsList
    {
        public static SettingInfo Port = new SettingInfo { Name = "ServiceControl.Audit/Port" };
        public static SettingInfo DatabaseMaintenancePort = new SettingInfo { Name = "ServiceControl.Audit/DatabaseMaintenancePort" };
        public static SettingInfo HostName = new SettingInfo { Name = "ServiceControl.Audit/HostName" };
        public static SettingInfo LogPath = new SettingInfo { Name = "ServiceControl.Audit/LogPath" };
        public static SettingInfo DBPath = new SettingInfo { Name = "ServiceControl.Audit/DBPath" };
        public static SettingInfo ForwardAuditMessages = new SettingInfo { Name = "ServiceControl.Audit/ForwardAuditMessages" };
        public static SettingInfo TransportType = new SettingInfo { Name = "ServiceControl.Audit/TransportType" };
        public static SettingInfo AuditQueue = new SettingInfo { Name = "ServiceBus/AuditQueue" };
        public static SettingInfo AuditLogQueue = new SettingInfo { Name = "ServiceBus/AuditLogQueue" };
        public static SettingInfo AuditRetentionPeriod = new SettingInfo { Name = "ServiceControl.Audit/AuditRetentionPeriod" };
        public static SettingInfo MaintenanceMode = new SettingInfo { Name = "ServiceControl.Audit/MaintenanceMode" };
    }
}