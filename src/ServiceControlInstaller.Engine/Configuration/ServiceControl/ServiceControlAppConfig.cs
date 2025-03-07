namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using System;
    using System.Data.Common;
    using System.IO;
    using Instances;

    public class ServiceControlAppConfig : AppConfig
    {
        public ServiceControlAppConfig(IServiceControlInstance instance) : base(Path.Combine(instance.InstallPath, $"{Constants.ServiceControlExe}.config"))
        {
            details = instance;
        }

        protected override void UpdateSettings()
        {
            Config.ConnectionStrings.ConnectionStrings.Set("NServiceBus/Transport", UpdateConnectionString());
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

        string UpdateConnectionString()
        {
            var connectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = details.ConnectionString };

            MigrateLicensingComponentRabbitMqManagementApiSettings(connectionStringBuilder);

            return connectionStringBuilder.ConnectionString;
        }

        void MigrateLicensingComponentRabbitMqManagementApiSettings(DbConnectionStringBuilder connectionStringBuilder)
        {
            if (!details.TransportPackage.Name.Contains("rabbitmq", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var settings = Config.AppSettings.Settings;

            var legacySetting = settings[ServiceControlSettings.LicensingComponentRabbitMqManagementApiUrl.Name];
            if (legacySetting is not null && !connectionStringBuilder.ContainsKey("ManagementApiUrl"))
            {
                connectionStringBuilder.Add("ManagementApiUrl", legacySetting.Value);
            }

            legacySetting = settings[ServiceControlSettings.LicensingComponentRabbitMqManagementApiUsername.Name];
            if (legacySetting is not null && !connectionStringBuilder.ContainsKey("ManagementApiUserName"))
            {
                connectionStringBuilder.Add("ManagementApiUserName", legacySetting.Value);
            }

            legacySetting = settings[ServiceControlSettings.LicensingComponentRabbitMqManagementApiPassword.Name];
            if (legacySetting is not null && !connectionStringBuilder.ContainsKey("ManagementApiPassword"))
            {
                connectionStringBuilder.Add("ManagementApiPassword", legacySetting.Value);
            }
        }

        readonly IServiceControlInstance details;
    }
}