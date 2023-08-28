namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using System;

    // See Compatibility.cs for version switching that isn't related to Settings
    public static class ServiceControlSettings
    {
        public static readonly SettingInfo VirtualDirectory = new SettingInfo { Name = "ServiceControl/VirtualDirectory" };
        public static readonly SettingInfo Port = new SettingInfo { Name = "ServiceControl/Port" };
        public static readonly SettingInfo DatabaseMaintenancePort = new SettingInfo
        {
            Name = "ServiceControl/DatabaseMaintenancePort",
            SupportedFrom = new Version(2, 0, 0)
        };
        public static readonly SettingInfo HostName = new SettingInfo { Name = "ServiceControl/HostName" };
        public static readonly SettingInfo LogPath = new SettingInfo { Name = "ServiceControl/LogPath" };
        public static readonly SettingInfo DBPath = new SettingInfo { Name = "ServiceControl/DBPath" };
        public static readonly SettingInfo ForwardAuditMessages = new SettingInfo
        {
            Name = "ServiceControl/ForwardAuditMessages",
            RemovedFrom = new Version(4, 0, 0)
        };

        public static readonly SettingInfo ForwardErrorMessages = new SettingInfo
        {
            Name = "ServiceControl/ForwardErrorMessages",
            SupportedFrom = new Version(1, 11, 2)
        };

        public static readonly SettingInfo TransportType = new SettingInfo { Name = "ServiceControl/TransportType" };
        public static readonly SettingInfo PersistenceType = new SettingInfo { Name = "ServiceControl/PersistenceType" };
        public static readonly SettingInfo AuditQueue = new SettingInfo
        {
            Name = "ServiceBus/AuditQueue",
            RemovedFrom = new Version(4, 0, 0)
        };
        public static readonly SettingInfo ErrorQueue = new SettingInfo { Name = "ServiceBus/ErrorQueue" };
        public static readonly SettingInfo ErrorLogQueue = new SettingInfo { Name = "ServiceBus/ErrorLogQueue" };
        public static readonly SettingInfo AuditLogQueue = new SettingInfo
        {
            Name = "ServiceBus/AuditLogQueue",
            RemovedFrom = new Version(4, 0, 0)
        };

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

        public static SettingInfo RemoteInstances = new SettingInfo
        {
            Name = "ServiceControl/RemoteInstances",
            SupportedFrom = new Version(1, 47, 0)
        };

        public static SettingInfo EnableFullTextSearchOnBodies = new SettingInfo
        {
            Name = "ServiceControl/EnableFullTextSearchOnBodies",
            SupportedFrom = new Version(4, 17, 0)
        };
    }
}
