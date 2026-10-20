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
        public static readonly SettingInfo DatabaseConnectionString = new() { Name = "ServiceControl/Database/ConnectionString" };
        public static readonly SettingInfo RavenDBConnectionString = new() { Name = "ServiceControl/RavenDB/ConnectionString" };
        public static readonly SettingInfo MessageBodyStorageType = new() { Name = "ServiceControl/MessageBody/StorageType" };
        public static readonly SettingInfo MessageBodyFileSystemStoragePath = new() { Name = "ServiceControl/MessageBody/FileSystem/StoragePath" };
        public static readonly SettingInfo MessageBodyAzureConnectionString = new() { Name = "ServiceControl/MessageBody/Azure/ConnectionString" };
        public static readonly SettingInfo MessageBodyAzureServiceUri = new() { Name = "ServiceControl/MessageBody/Azure/ServiceUri" };
        public static readonly SettingInfo MessageBodyAzureManagedIdentityClientId = new() { Name = "ServiceControl/MessageBody/Azure/ManagedIdentityClientId" };
        public static readonly SettingInfo MessageBodyAzureAuthorityHost = new() { Name = "ServiceControl/MessageBody/Azure/AuthorityHost" };
        public static readonly SettingInfo MessageBodyAzureContainerName = new() { Name = "ServiceControl/MessageBody/Azure/ContainerName" };
        public static readonly SettingInfo MessageBodyS3BucketName = new() { Name = "ServiceControl/MessageBody/S3/BucketName" };
        public static readonly SettingInfo MessageBodyS3KeyPrefix = new() { Name = "ServiceControl/MessageBody/S3/KeyPrefix" };
        public static readonly SettingInfo MessageBodyS3Region = new() { Name = "ServiceControl/MessageBody/S3/Region" };
        public static readonly SettingInfo MessageBodyS3ServiceUrl = new() { Name = "ServiceControl/MessageBody/S3/ServiceUrl" };
        public static readonly SettingInfo MessageBodyS3AccessKeyId = new() { Name = "ServiceControl/MessageBody/S3/AccessKeyId" };
        public static readonly SettingInfo MessageBodyS3SecretAccessKey = new() { Name = "ServiceControl/MessageBody/S3/SecretAccessKey" };
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

        public static SettingInfo ShutdownTimeout = new()
        {
            Name = "ServiceControl/ShutdownTimeout",
            SupportedFrom = new SemanticVersion(6, 4, 1)
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

        public static readonly SettingInfo EnableIntegratedServicePulse = new()
        {
            Name = "ServiceControl/EnableIntegratedServicePulse",
            SupportedFrom = new SemanticVersion(6, 13, 0)
        };

        public static readonly SettingInfo HttpsEnabled = new()
        {
            Name = "ServiceControl/Https.Enabled",
            SupportedFrom = new SemanticVersion(6, 11, 0)
        };
    }
}
