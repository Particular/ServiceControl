namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using NuGet.Versioning;

    // See Compatibility.cs for version switching that isn't related to Settings
    public static class ServiceControlSettings
    {
        public static readonly SettingInfo InternalQueueName = new()
        {
            Name = "ServiceControl/InternalQueueName",
            RemovedFrom = new(5, 5, 0)
        };

        public static readonly SettingInfo InstanceName = new()
        {
            Name = "ServiceControl/InstanceName",
            SupportedFrom = new SemanticVersion(5, 5, 0)
        };

        public static readonly SettingInfo VirtualDirectory = new() { Name = "ServiceControl/VirtualDirectory" };
        public static readonly SettingInfo Port = new() { Name = "ServiceControl/Port" };
        public static readonly SettingInfo DatabaseMaintenancePort = new SettingInfo
        {
            Name = "ServiceControl/DatabaseMaintenancePort",
            SupportedFrom = new SemanticVersion(2, 0, 0)
        };
        public static readonly SettingInfo HostName = new() { Name = "ServiceControl/HostName" };
        public static readonly SettingInfo LogPath = new() { Name = "ServiceControl/LogPath" };
        public static readonly SettingInfo DBPath = new() { Name = "ServiceControl/DBPath" };
        public static readonly SettingInfo ForwardAuditMessages = new()
        {
            Name = "ServiceControl/ForwardAuditMessages",
            RemovedFrom = new SemanticVersion(4, 0, 0)
        };

        public static readonly SettingInfo ForwardErrorMessages = new()
        {
            Name = "ServiceControl/ForwardErrorMessages",
            SupportedFrom = new SemanticVersion(1, 11, 2)
        };

        public static readonly SettingInfo TransportType = new() { Name = "ServiceControl/TransportType" };
        public static readonly SettingInfo PersistenceType = new() { Name = "ServiceControl/PersistenceType" };
        public static readonly SettingInfo AuditQueue = new()
        {
            Name = "ServiceBus/AuditQueue",
            RemovedFrom = new SemanticVersion(4, 0, 0)
        };
        public static readonly SettingInfo ErrorQueue = new() { Name = "ServiceBus/ErrorQueue" };
        public static readonly SettingInfo ErrorLogQueue = new() { Name = "ServiceBus/ErrorLogQueue" };
        public static readonly SettingInfo AuditLogQueue = new()
        {
            Name = "ServiceBus/AuditLogQueue",
            RemovedFrom = new SemanticVersion(4, 0, 0)
        };

        public static SettingInfo AuditRetentionPeriod = new()
        {
            Name = "ServiceControl/AuditRetentionPeriod",
            SupportedFrom = new SemanticVersion(1, 12, 1)
        };

        public static SettingInfo ErrorRetentionPeriod = new()
        {
            Name = "ServiceControl/ErrorRetentionPeriod",
            SupportedFrom = new SemanticVersion(1, 12, 1)
        };

        public static SettingInfo HoursToKeepMessagesBeforeExpiring = new()
        {
            Name = "ServiceControl/HoursToKeepMessagesBeforeExpiring",
            RemovedFrom = new SemanticVersion(1, 12, 1)
        };

        public static SettingInfo MaintenanceMode = new()
        {
            Name = "ServiceControl/MaintenanceMode",
            SupportedFrom = new SemanticVersion(1, 18, 1)
        };

        public static SettingInfo RemoteInstances = new()
        {
            Name = "ServiceControl/RemoteInstances",
            SupportedFrom = new SemanticVersion(1, 47, 0)
        };

        public static SettingInfo EnableFullTextSearchOnBodies = new()
        {
            Name = "ServiceControl/EnableFullTextSearchOnBodies",
            SupportedFrom = new SemanticVersion(4, 17, 0)
        };

        public static readonly SettingInfo LicensingComponentRabbitMqManagementApiUrl = new()
        {
            Name = "LicensingComponent/RabbitMQ/ApiUrl",
            RemovedFrom = new SemanticVersion(6, 5, 0)
        };

        public static readonly SettingInfo LicensingComponentRabbitMqManagementApiUsername = new()
        {
            Name = "LicensingComponent/RabbitMQ/UserName",
            RemovedFrom = new SemanticVersion(6, 5, 0)
        };

        public static readonly SettingInfo LicensingComponentRabbitMqManagementApiPassword = new()
        {
            Name = "LicensingComponent/RabbitMQ/Password",
            RemovedFrom = new SemanticVersion(6, 5, 0)
        };
    }
}