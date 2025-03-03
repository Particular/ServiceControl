namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.IO;
    using System.Linq;
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

            // Retired settings
            settings.RemoveIfRetired(ServiceControlSettings.AuditQueue, version);
            settings.RemoveIfRetired(ServiceControlSettings.AuditLogQueue, version);
            settings.RemoveIfRetired(ServiceControlSettings.ForwardAuditMessages, version);
            settings.RemoveIfRetired(ServiceControlSettings.InternalQueueName, version);
            settings.RemoveIfRetired(ServiceControlSettings.RabbitMqManagementApiUrl, version);
            settings.RemoveIfRetired(ServiceControlSettings.RabbitMqManagementApiUsername, version);
            settings.RemoveIfRetired(ServiceControlSettings.RabbitMqManagementApiPassword, version);

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
            var kvpList = new DbConnectionStringBuilder { ConnectionString = details.ConnectionString }
                .OfType<KeyValuePair<string, object>>()
                .Select(kvp => new KeyValuePair<string, string>(kvp.Key, kvp.Value.ToString()))
                .ToList();

            MigrateRabbitMqManagementApiSettings(kvpList);

            return string.Join(";", kvpList.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        }

        void MigrateRabbitMqManagementApiSettings(IList<KeyValuePair<string, string>> connectionStringPairs)
        {
            if (!details.TransportPackage.Name.Contains("rabbitmq", StringComparison.OrdinalIgnoreCase) ||
                connectionStringPairs.Any(kvp => kvp.Key.Equals("ManagementApiUrl", StringComparison.OrdinalIgnoreCase) || kvp.Key.Equals("ManagementApiUserName", StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var settings = Config.AppSettings.Settings;

            var legacySetting = settings["LicensingComponent/RabbitMQ/ApiUrl"];
            if (legacySetting is not null)
            {
                connectionStringPairs.Add(new KeyValuePair<string, string>("ManagementApiUrl", legacySetting.Value));
            }

            legacySetting = settings["LicensingComponent/RabbitMQ/UserName"];
            if (legacySetting is not null)
            {
                connectionStringPairs.Add(new KeyValuePair<string, string>("ManagementApiUserName", legacySetting.Value));
            }

            legacySetting = settings["LicensingComponent/RabbitMQ/Password"];
            if (legacySetting is not null)
            {
                connectionStringPairs.Add(new KeyValuePair<string, string>("ManagementApiPassword", legacySetting.Value));
            }
        }

        readonly IServiceControlInstance details;
    }
}