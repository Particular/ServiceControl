namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using System;
    using System.Configuration;
    using System.Data.Common;
    using System.IO;
    using Instances;
    using NuGet.Versioning;

    public class ServiceControlAppConfig : AppConfig
    {
        public ServiceControlAppConfig(IServiceControlInstance instance) : base(Path.Combine(instance.InstallPath, $"{Constants.ServiceControlExe}.config"))
        {
            details = instance;
        }

        protected override void UpdateSettings()
        {
            UpdateConnectionString();
            Config.ConnectionStrings.ConnectionStrings.Set("NServiceBus/Transport", details.ConnectionString);

            var settings = Config.AppSettings.Settings;
            var version = details.Version;

            settings.Set(ServiceControlSettings.InstanceName, details.InstanceName, version);
            settings.Set(ServiceControlSettings.VirtualDirectory, details.VirtualDirectory);
            settings.Set(ServiceControlSettings.Port, details.Port.ToString());
            settings.Set(ServiceControlSettings.DatabaseMaintenancePort, details.DatabaseMaintenancePort.ToString(), version);
            settings.Set(ServiceControlSettings.HostName, details.HostName);
            settings.Set(ServiceControlSettings.LogPath, details.LogPath);
            settings.Set(ServiceControlSettings.DBPath, details.DBPath);
            settings.Set(ServiceControlSettings.ForwardErrorMessages, details.ForwardErrorMessages.ToString(), version);
            settings.Set(ServiceControlSettings.TransportType, details.TransportPackage.Name, version);
            settings.Set(ServiceControlSettings.PersistenceType, details.PersistenceManifest.Name); // TODO: Why is it set here AND at ServiceControlInstance.ApplySettingsChanges 🤬
            settings.Set(ServiceControlSettings.ErrorQueue, details.ErrorQueue);
            settings.Set(ServiceControlSettings.ErrorLogQueue, details.ForwardErrorMessages ? details.ErrorLogQueue : null);
            settings.Set(ServiceControlSettings.AuditRetentionPeriod, details.AuditRetentionPeriod.ToString(), version);
            settings.Set(ServiceControlSettings.ErrorRetentionPeriod, details.ErrorRetentionPeriod.ToString(), version);
            settings.Set(ServiceControlSettings.EnableFullTextSearchOnBodies, details.EnableFullTextSearchOnBodies.ToString(), version);
            settings.Set(ServiceControlSettings.RemoteInstances, RemoteInstanceConverter.ToJson(details.RemoteInstances), version);

            // Windows services allow a maximum of 125 seconds when stopping a service.
            // When shutting down or restarting the OS we have no control over the
            // shutdown timeout. This is by the installer engine that is run _only_ on
            // Windows via SCMU or PowerShell
            settings.Set(ServiceControlSettings.ShutdownTimeout, "00:02:00", version);

            // Retired settings
            settings.RemoveIfRetired(ServiceControlSettings.AuditQueue, version);
            settings.RemoveIfRetired(ServiceControlSettings.AuditLogQueue, version);
            settings.RemoveIfRetired(ServiceControlSettings.ForwardAuditMessages, version);
            settings.RemoveIfRetired(ServiceControlSettings.InternalQueueName, version);
            settings.RemoveIfRetired(ServiceControlSettings.LicensingComponentRabbitMqManagementApiUrl, version);
            settings.RemoveIfRetired(ServiceControlSettings.LicensingComponentRabbitMqManagementApiUsername, version);
            settings.RemoveIfRetired(ServiceControlSettings.LicensingComponentRabbitMqManagementApiPassword, version);

            RemoveRavenDB35Settings(settings, version);
        }

        public override void EnableMaintenanceMode()
        {
            var settings = Config.AppSettings.Settings;
            settings.Set(ServiceControlSettings.MaintenanceMode, bool.TrueString, details.Version);
            Config.Save();
        }

        public override void DisableMaintenanceMode()
        {
            var settings = Config.AppSettings.Settings;
            settings.Remove(ServiceControlSettings.MaintenanceMode.Name);
            Config.Save();
        }

        public override void SetTransportType(string transportTypeName)
        {
            var settings = Config.AppSettings.Settings;
            var version = details.Version;
            settings.Set(ServiceControlSettings.TransportType, transportTypeName, version);
        }

        void UpdateConnectionString()
        {
            if (details.TransportPackage.Name.Contains("rabbitmq", StringComparison.OrdinalIgnoreCase))
            {
                MigrateLicensingComponentRabbitMqManagementApiSettings();
            }
        }

        void MigrateLicensingComponentRabbitMqManagementApiSettings()
        {
            if (details.ConnectionString.StartsWith("amqp", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var shouldMigrate = VersionComparer.Version.Compare(details.Version, new SemanticVersion(6, 5, 0)) >= 0;

            if (shouldMigrate)
            {
                var connectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = details.ConnectionString };
                var settings = Config.AppSettings.Settings;

                MigrateSetting(connectionStringBuilder, settings[ServiceControlSettings.LicensingComponentRabbitMqManagementApiUrl.Name], "ManagementApiUrl");
                MigrateSetting(connectionStringBuilder, settings[ServiceControlSettings.LicensingComponentRabbitMqManagementApiUsername.Name], "ManagementApiUserName");
                MigrateSetting(connectionStringBuilder, settings[ServiceControlSettings.LicensingComponentRabbitMqManagementApiPassword.Name], "ManagementApiPassword");

                details.ConnectionString = connectionStringBuilder.ConnectionString;
            }

            static void MigrateSetting(DbConnectionStringBuilder connectionStringBuilder, KeyValueConfigurationElement setting, string connectionStringSettingName)
            {
                if (setting is not null && !connectionStringBuilder.ContainsKey(connectionStringSettingName))
                {
                    connectionStringBuilder.Add(connectionStringSettingName, setting.Value);
                }
            }
        }

        readonly IServiceControlInstance details;
    }
}