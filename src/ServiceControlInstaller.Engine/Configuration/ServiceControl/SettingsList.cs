namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using System;

    // See Compatibility.cs for version switching that isn't related to Settings

    public static class SettingsList
    {
        public static SettingInfo VirtualDirectory = new SettingInfo {Name = "ServiceControl/VirtualDirectory"};
        public static SettingInfo Port = new SettingInfo {Name = "ServiceControl/Port"};
        public static SettingInfo DatabaseMaintenancePort = new SettingInfo
        {
            Name = "ServiceControl/DatabaseMaintenancePort",
            SupportedFrom = new Version(2, 0, 0)
        };
        public static SettingInfo HostName = new SettingInfo {Name = "ServiceControl/HostName"};
        public static SettingInfo LogPath = new SettingInfo {Name = "ServiceControl/LogPath"};
        public static SettingInfo DBPath = new SettingInfo {Name = "ServiceControl/DBPath"};
        public static SettingInfo ForwardAuditMessages = new SettingInfo {Name = "ServiceControl/ForwardAuditMessages"};

        public static SettingInfo ForwardErrorMessages = new SettingInfo
        {
            Name = "ServiceControl/ForwardErrorMessages",
            SupportedFrom = new Version(1, 11, 2)
        };

        public static SettingInfo TransportType = new SettingInfo {Name = "ServiceControl/TransportType"};
        public static SettingInfo AuditQueue = new SettingInfo {Name = "ServiceBus/AuditQueue"};
        public static SettingInfo ErrorQueue = new SettingInfo {Name = "ServiceBus/ErrorQueue"};
        public static SettingInfo ErrorLogQueue = new SettingInfo {Name = "ServiceBus/ErrorLogQueue"};
        public static SettingInfo AuditLogQueue = new SettingInfo {Name = "ServiceBus/AuditLogQueue"};

        public static SettingInfo AuditRetentionPeriod = new SettingInfo
        {
            Name = "ServiceControl/AuditRetentionPeriod",
            SupportedFrom = new Version(1, 12, 1)
        };

        public static SettingInfo ErrorRetentionPeriod = new SettingInfo
        {
            Name = "ServiceControl/ErrorRetentionPeriod",
            SupportedFrom = new Version(1, 12, 1)
        };

        public static SettingInfo HoursToKeepMessagesBeforeExpiring = new SettingInfo
        {
            Name = "ServiceControl/HoursToKeepMessagesBeforeExpiring",
            RemovedFrom = new Version(1, 12, 1)
        };

        public static SettingInfo MaintenanceMode = new SettingInfo
        {
            Name = "ServiceControl/MaintenanceMode",
            SupportedFrom = new Version(1, 18, 1)
        };
    }
}