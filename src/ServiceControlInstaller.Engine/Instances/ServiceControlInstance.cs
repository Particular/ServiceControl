// ReSharper disable MemberCanBePrivate.Global

namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using Configuration;
    using Configuration.ServiceControl;
    using FileSystem;
    using Queues;
    using Services;
    using Validation;

    public class ServiceControlInstance : ServiceControlBaseService, IServiceControlInstance
    {
        public ServiceControlInstance(WindowsServiceController service) : base(service)
        {
        }

        protected override string BaseServiceName => "ServiceControl";

        public TimeSpan? AuditRetentionPeriod { get; set; }

        public List<RemoteInstanceSetting> RemoteInstances { get; set; } = new List<RemoteInstanceSetting>();

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

        protected override void ValidatePaths()
        {
            try
            {
                new PathsValidator(this).RunValidation(false);
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
            HostName = AppConfig.Read(ServiceControlSettings.HostName, "localhost");
            Port = AppConfig.Read(ServiceControlSettings.Port, 33333);
            DatabaseMaintenancePort = AppConfig.Read<int?>(ServiceControlSettings.DatabaseMaintenancePort, null);
            VirtualDirectory = AppConfig.Read(ServiceControlSettings.VirtualDirectory, (string)null);
            LogPath = AppConfig.Read(ServiceControlSettings.LogPath, DefaultLogPath());
            DBPath = AppConfig.Read(ServiceControlSettings.DBPath, DefaultDBPath());
            AuditQueue = AppConfig.Read(ServiceControlSettings.AuditQueue, (string)null);
            AuditLogQueue = AppConfig.Read(ServiceControlSettings.AuditLogQueue, (string)null);
            ForwardAuditMessages = AppConfig.Read(ServiceControlSettings.ForwardAuditMessages, false);
            ForwardErrorMessages = AppConfig.Read(ServiceControlSettings.ForwardErrorMessages, false);
            InMaintenanceMode = AppConfig.Read(ServiceControlSettings.MaintenanceMode, false);
            ErrorQueue = AppConfig.Read(ServiceControlSettings.ErrorQueue, "error");
            ErrorLogQueue = AppConfig.Read(ServiceControlSettings.ErrorLogQueue, $"{ErrorQueue}.log");
            TransportPackage = DetermineTransportPackage();
            ConnectionString = ReadConnectionString();
            Description = GetDescription();
            ServiceAccount = Service.Account;

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

            if (AuditRetentionPeriod.HasValue)
            {
                settings.Set(ServiceControlSettings.AuditRetentionPeriod, AuditRetentionPeriod.Value.ToString(), Version);    
            }
            
            settings.Set(ServiceControlSettings.ErrorRetentionPeriod, ErrorRetentionPeriod.ToString(), Version);
            settings.RemoveIfRetired(ServiceControlSettings.HoursToKeepMessagesBeforeExpiring, Version);
            settings.Set(ServiceControlSettings.AuditQueue, AuditQueue, Version);
            settings.Set(ServiceControlSettings.ErrorQueue, ErrorQueue);
            settings.Set(ServiceControlSettings.AuditLogQueue, AuditLogQueue, Version);
            settings.Set(ServiceControlSettings.ErrorLogQueue, ErrorLogQueue, Version);

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
        }

        protected override IEnumerable<string> GetDatabaseIndexes()
        {
            return AppConfig.RavenDataPaths();
        }
    }
}