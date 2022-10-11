namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using System;

    public static class AuditInstanceSettingsList
    {
        public static readonly SettingInfo Port = new SettingInfo { Name = "ServiceControl.Audit/Port" };
        public static readonly SettingInfo DatabaseMaintenancePort = new SettingInfo { Name = "ServiceControl.Audit/DatabaseMaintenancePort" };
        public static readonly SettingInfo HostName = new SettingInfo { Name = "ServiceControl.Audit/HostName" };
        public static readonly SettingInfo LogPath = new SettingInfo { Name = "ServiceControl.Audit/LogPath" };
        public static readonly SettingInfo DBPath = new SettingInfo { Name = "ServiceControl.Audit/DBPath" };
        public static readonly SettingInfo ForwardAuditMessages = new SettingInfo { Name = "ServiceControl.Audit/ForwardAuditMessages" };
        public static readonly SettingInfo TransportType = new SettingInfo { Name = "ServiceControl.Audit/TransportType" };
        public static readonly SettingInfo PersistenceType = new SettingInfo { Name = "ServiceControl.Audit/PersistenceType" };
        public static readonly SettingInfo AuditQueue = new SettingInfo { Name = "ServiceBus/AuditQueue" };
        public static readonly SettingInfo AuditLogQueue = new SettingInfo { Name = "ServiceBus/AuditLogQueue" };
        public static readonly SettingInfo AuditRetentionPeriod = new SettingInfo { Name = "ServiceControl.Audit/AuditRetentionPeriod" };
        public static readonly SettingInfo MaintenanceMode = new SettingInfo { Name = "ServiceControl.Audit/MaintenanceMode" };
        public static readonly SettingInfo ServiceControlQueueAddress = new SettingInfo { Name = "ServiceControl.Audit/ServiceControlQueueAddress" };
        public static readonly SettingInfo EnableFullTextSearchOnBodies = new SettingInfo { Name = "ServiceControl.Audit/EnableFullTextSearchOnBodies", SupportedFrom = new Version(4, 17, 0) };
    }
}