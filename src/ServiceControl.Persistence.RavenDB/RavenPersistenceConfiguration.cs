namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.IO;
    using System.Reflection;
    using Configuration;
    using CustomChecks;
    using Particular.LicensingComponent.Contracts;
    using ServiceControl.Infrastructure;

    class RavenPersistenceConfiguration : PersistenceConfiguration, IPersistenceConfiguration
    {
        const string AuditRetentionPeriodKey = "AuditRetentionPeriod";
        const string ErrorRetentionPeriodKey = "ErrorRetentionPeriod";
        const string EventsRetentionPeriodKey = "EventsRetentionPeriod";
        const string ExternalIntegrationsDispatchingBatchSizeKey = "ExternalIntegrationsDispatchingBatchSize";
        const string MaintenanceModeKey = "MaintenanceMode";

        public PersistenceSettings CreateSettings(SettingsRootNamespace settingsRootNamespace)
        {
            var ravenDbLogLevel = SettingsReader.Read(settingsRootNamespace, RavenBootstrapper.RavenDbLogLevelKey, "Warn");
            var logsMode = RavenDbLogLevelToLogsModeMapper.Map(ravenDbLogLevel, LoggerUtil.CreateStaticLogger<RavenPersistenceConfiguration>());

            var settings = new RavenPersisterSettings();

            LoadCommonConfiguration(settings, settingsRootNamespace);

            settings.ConnectionString = SettingsReader.Read(settingsRootNamespace, RavenBootstrapper.ConnectionStringKey, settings.ConnectionString);
            settings.ClientCertificatePath = SettingsReader.Read<string>(settingsRootNamespace, RavenBootstrapper.ClientCertificatePathKey);
            settings.ClientCertificateBase64 = SettingsReader.Read<string>(settingsRootNamespace, RavenBootstrapper.ClientCertificateBase64Key);
            settings.ClientCertificatePassword = SettingsReader.Read<string>(settingsRootNamespace, RavenBootstrapper.ClientCertificatePasswordKey);
            settings.DatabaseName = SettingsReader.Read(settingsRootNamespace, RavenBootstrapper.DatabaseNameKey, RavenPersisterSettings.DatabaseNameDefault);
            settings.DatabasePath = SettingsReader.Read(settingsRootNamespace, RavenBootstrapper.DatabasePathKey, DefaultDatabaseLocation());
            settings.DatabaseMaintenancePort = SettingsReader.Read(settingsRootNamespace, RavenBootstrapper.DatabaseMaintenancePortKey, RavenPersisterSettings.DatabaseMaintenancePortDefault);
            settings.ExpirationProcessTimerInSeconds = SettingsReader.Read(settingsRootNamespace, RavenBootstrapper.ExpirationProcessTimerInSecondsKey, 600);
            settings.ErrorRetentionPeriod = GetRequiredSetting<TimeSpan>(settingsRootNamespace, ErrorRetentionPeriodKey);
            settings.EventsRetentionPeriod = SettingsReader.Read(settingsRootNamespace, EventsRetentionPeriodKey, TimeSpan.FromDays(14));
            settings.AuditRetentionPeriod = SettingsReader.Read(settingsRootNamespace, AuditRetentionPeriodKey, TimeSpan.Zero);
            settings.ExternalIntegrationsDispatchingBatchSize = SettingsReader.Read(settingsRootNamespace, ExternalIntegrationsDispatchingBatchSizeKey, 100);
            settings.MaintenanceMode = SettingsReader.Read(settingsRootNamespace, MaintenanceModeKey, false);
            settings.LogPath = SettingsReader.Read(settingsRootNamespace, RavenBootstrapper.LogsPathKey, DefaultLogLocation());
            settings.LogsMode = logsMode;
            settings.EnableFullTextSearchOnBodies = SettingsReader.Read(settingsRootNamespace, "EnableFullTextSearchOnBodies", true);
            settings.ThroughputDatabaseName = SettingsReader.Read(ThroughputSettings.SettingsNamespace, ThroughputSettings.DatabaseNameKey, ThroughputSettings.DefaultDatabaseName);

            CheckFreeDiskSpace.Validate(settings);
            CheckMinimumStorageRequiredForIngestion.Validate(settings);
            return settings;
        }

        // SC installer always populates DBPath in app.config on installation/change/upgrade so this will only be used when
        // debugging or if the entry is removed manually. In those circumstances default to the folder containing the exe
        static string DefaultDatabaseLocation()
        {
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            return Path.Combine(Path.GetDirectoryName(assemblyLocation), ".db");
        }

        // SC installer always populates LogPath in app.config on installation/change/upgrade so this will only be used when
        // debugging or if the entry is removed manually. In those circumstances default to the folder containing the exe
        static string DefaultLogLocation()
        {
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            return Path.Combine(Path.GetDirectoryName(assemblyLocation), ".logs");
        }

        public IPersistence Create(PersistenceSettings settings)
        {
            var specificSettings = (RavenPersisterSettings)settings;
            return new RavenPersistence(specificSettings);
        }
    }
}