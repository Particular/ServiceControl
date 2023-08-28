namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Configuration;
    using Configuration.ServiceControl;
    using FileSystem;
    using Queues;
    using Services;
    using Validation;

    public class ServiceControlInstance : ServiceControlBaseService, IServiceControlInstance
    {
        public ServiceControlInstance(IWindowsServiceController service)
            : this(service, Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))
        {
        }

        public ServiceControlInstance(IWindowsServiceController service, string deploymentCachePath) : base(service)
        {
            this.deploymentCachePath = deploymentCachePath;

            Reload();
        }

        protected override string BaseServiceName => "ServiceControl";

        public TimeSpan? AuditRetentionPeriod { get; set; }

        public List<RemoteInstanceSetting> RemoteInstances { get; set; } = new List<RemoteInstanceSetting>();

        public PersistenceManifest PersistenceManifest { get; set; }

        public void AddRemoteInstance(string apiUri)
        {
            if (RemoteInstances.All(x => string.Compare(x.ApiUri, apiUri, StringComparison.InvariantCultureIgnoreCase) != 0))
            {
                RemoteInstances.Add(new RemoteInstanceSetting
                {
                    ApiUri = apiUri
                });
            }
        }

        protected override string GetTransportTypeSetting()
        {
            return AppConfig.Read(ServiceControlSettings.TransportType, ServiceControlCoreTransports.All.Single(t => t.Default).TypeName).Trim();
        }

        protected override AppConfig CreateAppConfig()
        {
            return new ServiceControlAppConfig(this);
        }

        public override void RunQueueCreation()
        {
            QueueCreation.RunQueueCreation(this);
        }

        public bool IsAuditQueueDisabled() => IsQueueDisabled(AuditQueue);

        public bool IsErrorQueueDisabled() => IsQueueDisabled(ErrorQueue);

        protected override void ValidateQueueNames()
        {
            try
            {
                QueueNameValidator.Validate(this);
            }
            catch (EngineValidationException ex)
            {
                ReportCard.Errors.Add(ex.Message);
            }
        }

        protected override async Task ValidatePaths()
        {
            try
            {
                await new PathsValidator(this).RunValidation(false).ConfigureAwait(false);
            }
            catch (EngineValidationException ex)
            {
                ReportCard.Errors.Add(ex.Message);
            }
        }

        protected override void ValidateConnectionString()
        {
            try
            {
                ConnectionStringValidator.Validate(this);
            }
            catch (EngineValidationException ex)
            {
                ReportCard.Errors.Add(ex.Message);
            }
        }

        public override void Reload()
        {
            Service.Refresh();

            AppConfig = CreateAppConfig();
            HostName = AppConfig.Read(ServiceControlSettings.HostName, "localhost");
            Port = AppConfig.Read(ServiceControlSettings.Port, 33333);
            DatabaseMaintenancePort = AppConfig.Read<int?>(ServiceControlSettings.DatabaseMaintenancePort, null);
            VirtualDirectory = AppConfig.Read(ServiceControlSettings.VirtualDirectory, (string)null);
            LogPath = AppConfig.Read(ServiceControlSettings.LogPath, DefaultLogPath());
            DBPath = AppConfig.Read(ServiceControlSettings.DBPath, DefaultDBPath());
            AuditQueue = AppConfig.Read(ServiceControlSettings.AuditQueue, (string)null);
            InMaintenanceMode = AppConfig.Read(ServiceControlSettings.MaintenanceMode, false);
            ErrorQueue = AppConfig.Read(ServiceControlSettings.ErrorQueue, "error");
            TransportPackage = DetermineTransportPackage();
            ConnectionString = ReadConnectionString();
            Description = GetDescription();
            ServiceAccount = Service.Account;

            var zipInfo = ServiceControlZipInfo.Find(deploymentCachePath);
            var manifests = ServiceControlPersisters.LoadAllManifests(zipInfo.FilePath);
            var persistenceType = AppConfig.Read<string>(ServiceControlSettings.PersistenceType, null);

            if (string.IsNullOrEmpty(persistenceType))
            {
                PersistenceManifest = manifests.Single(m => m.Name == ServiceControlNewInstance.DefaultPersister);
            }
            else
            {
                PersistenceManifest = manifests.Single(m => m.TypeName == persistenceType);
            }

            ForwardErrorMessages = AppConfig.Read(ServiceControlSettings.ForwardErrorMessages, false);
            if (ForwardErrorMessages)
            {
                ErrorLogQueue = AppConfig.Read(ServiceControlSettings.ErrorLogQueue, $"{ErrorQueue}.log");
            }

            ForwardAuditMessages = AppConfig.Read(ServiceControlSettings.ForwardAuditMessages, false);
            if (ForwardAuditMessages)
            {
                AuditLogQueue = AppConfig.Read(ServiceControlSettings.AuditLogQueue, string.IsNullOrEmpty(AuditQueue) ? null : $"{AuditQueue}.log");
            }

            if (TimeSpan.TryParse(AppConfig.Read(ServiceControlSettings.ErrorRetentionPeriod, (string)null), out var errorRetentionPeriod))
            {
                ErrorRetentionPeriod = errorRetentionPeriod;
            }

            if (TimeSpan.TryParse(AppConfig.Read(ServiceControlSettings.AuditRetentionPeriod, (string)null), out var auditRetentionPeriod))
            {
                AuditRetentionPeriod = auditRetentionPeriod;
            }

            var remoteInstancesString = AppConfig.Read(ServiceControlSettings.RemoteInstances, default(string));
            if (!string.IsNullOrWhiteSpace(remoteInstancesString))
            {
                RemoteInstances = RemoteInstanceConverter.FromJson(remoteInstancesString);
            }

            EnableFullTextSearchOnBodies = AppConfig.Read(ServiceControlSettings.EnableFullTextSearchOnBodies, true);
        }

        protected override void ApplySettingsChanges(KeyValueConfigurationCollection settings)
        {
            if (!ForwardErrorMessages)
            {
                ErrorLogQueue = null;
            }

            if (!ForwardAuditMessages)
            {
                AuditLogQueue = null;
            }

            settings.Set(ServiceControlSettings.HostName, HostName);
            settings.Set(ServiceControlSettings.Port, Port.ToString());
            settings.Set(ServiceControlSettings.DatabaseMaintenancePort, DatabaseMaintenancePort.ToString(), Version);
            settings.Set(ServiceControlSettings.LogPath, LogPath);
            settings.Set(ServiceControlSettings.ForwardAuditMessages, ForwardAuditMessages.ToString(), Version);
            settings.Set(ServiceControlSettings.ForwardErrorMessages, ForwardErrorMessages.ToString(), Version);
            settings.Set(ServiceControlSettings.AuditRetentionPeriod, AuditRetentionPeriod.ToString(), Version);
            settings.Set(ServiceControlSettings.ErrorRetentionPeriod, ErrorRetentionPeriod.ToString(), Version);
            settings.RemoveIfRetired(ServiceControlSettings.HoursToKeepMessagesBeforeExpiring, Version);
            settings.Set(ServiceControlSettings.AuditQueue, AuditQueue, Version);
            settings.Set(ServiceControlSettings.ErrorQueue, ErrorQueue);
            settings.Set(ServiceControlSettings.AuditLogQueue, AuditLogQueue, Version);
            settings.Set(ServiceControlSettings.ErrorLogQueue, ErrorLogQueue, Version);
            settings.Set(ServiceControlSettings.EnableFullTextSearchOnBodies, EnableFullTextSearchOnBodies.ToString(), Version);
            settings.Set(ServiceControlSettings.PersistenceType, PersistenceManifest.TypeName);

            if (RemoteInstances != null)
            {
                if (Compatibility.RemoteInstancesDoNotNeedQueueAddress.SupportedFrom <= Version)
                {
                    foreach (var instance in RemoteInstances)
                    {
                        instance.QueueAddress = null;
                    }
                }

                settings.Set(ServiceControlSettings.RemoteInstances, RemoteInstanceConverter.ToJson(RemoteInstances), Version);
            }
        }

        public override void UpgradeFiles(string zipFilePath)
        {
            FileUtils.DeleteDirectory(InstallPath, true, true, "license", $"{Constants.ServiceControlExe}.config");
            FileUtils.UnzipToSubdirectory(zipFilePath, InstallPath, "ServiceControl");
            FileUtils.UnzipToSubdirectory(zipFilePath, InstallPath, $@"Transports\{TransportPackage.ZipName}");
            FileUtils.UnzipToSubdirectory(zipFilePath, InstallPath, $@"Persisters\{PersistenceManifest.Name}");
        }

        protected override IEnumerable<string> GetPersistencePathsToCleanUp()
        {
            string[] keys =
            {
                "Raven/IndexStoragePath",
                "Raven/CompiledIndexCacheDirectory",
                "Raven/Esent/LogsPath",
                ServiceControlSettings.DBPath.Name
            };

            var settings = AppConfig.Config.AppSettings.Settings;
            foreach (var key in keys)
            {
                if (!settings.AllKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                var folderpath = settings[key].Value;
                yield return folderpath.StartsWith("~") //Raven uses ~ to indicate path is relative to bin folder e.g. ~/Data/Logs
                    ? Path.Combine(InstallPath, folderpath.Remove(0, 1))
                    : folderpath;
            }
        }

        readonly string deploymentCachePath;
    }
}