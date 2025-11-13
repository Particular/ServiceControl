namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.IO;
    using System.Reflection;
    using Configuration;
    using CustomChecks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Options;
    using Particular.LicensingComponent.Contracts;
    using ServiceControl.Infrastructure;

    sealed class RavenPersistenceConfiguration(IConfiguration configuration) : IConfigureOptions<RavenPersisterSettings>
    {
        public const string DataSpaceRemainingThresholdKey = "DataSpaceRemainingThreshold";
        const string AuditRetentionPeriodKey = "AuditRetentionPeriod";
        const string ErrorRetentionPeriodKey = "ErrorRetentionPeriod";
        const string EventsRetentionPeriodKey = "EventsRetentionPeriod";
        const string ExternalIntegrationsDispatchingBatchSizeKey = "ExternalIntegrationsDispatchingBatchSize";
        const string MaintenanceModeKey = "MaintenanceMode";

        const string DatabasePathKey = "DbPath";
        const string HostNameKey = "HostName";
        const string DatabaseMaintenancePortKey = "DatabaseMaintenancePort";
        const string ExpirationProcessTimerInSecondsKey = "ExpirationProcessTimerInSeconds";
        const string ConnectionStringKey = "RavenDB:ConnectionString";
        const string ClientCertificatePathKey = "RavenDB:ClientCertificatePath";
        const string ClientCertificateBase64Key = "RavenDB:ClientCertificateBase64";
        const string ClientCertificatePasswordKey = "RavenDB:ClientCertificatePassword";
        public const string MinimumStorageLeftRequiredForIngestionKey = "MinimumStorageLeftRequiredForIngestion";
        const string DatabaseNameKey = "RavenDB:DatabaseName";
        const string LogsPathKey = "LogPath";
        const string RavenDbLogLevelKey = "RavenDBLogLevel";

        public void Configure(RavenPersisterSettings options)
        {
            IConfigurationSection s = configuration.GetSection("ServiceControl");

            var ravenDbLogLevel = s.GetValue(RavenDbLogLevelKey, "Warn");
            var logsMode = RavenDbLogLevelToLogsModeMapper.Map(ravenDbLogLevel, LoggerUtil.CreateStaticLogger<RavenPersistenceConfiguration>());

            options.ConnectionString = s.GetValue<string>(ConnectionStringKey);
            options.ClientCertificatePath = s.GetValue<string>(ClientCertificatePathKey);
            options.ClientCertificateBase64 = s.GetValue<string>(ClientCertificateBase64Key);
            options.ClientCertificatePassword = s.GetValue<string>(ClientCertificatePasswordKey);
            options.DatabaseName = s.GetValue(DatabaseNameKey, RavenPersisterSettings.DatabaseNameDefault);
            options.DatabasePath = s.GetValue(DatabasePathKey, DefaultDatabaseLocation());
            options.DatabaseMaintenancePort = s.GetValue(DatabaseMaintenancePortKey, RavenPersisterSettings.DatabaseMaintenancePortDefault);
            options.ExpirationProcessTimerInSeconds = s.GetValue(ExpirationProcessTimerInSecondsKey, RavenPersisterSettings.ExpirationProcessTimerInSecondsDefault);
            options.MinimumStorageLeftRequiredForIngestion = s.GetValue(MinimumStorageLeftRequiredForIngestionKey, CheckMinimumStorageRequiredForIngestion.MinimumStorageLeftRequiredForIngestionDefault);
            options.DataSpaceRemainingThreshold = s.GetValue(DataSpaceRemainingThresholdKey, CheckFreeDiskSpace.DataSpaceRemainingThresholdDefault);
            options.ErrorRetentionPeriod = s.GetValue<TimeSpan>(ErrorRetentionPeriodKey);
            options.EventsRetentionPeriod = s.GetValue(EventsRetentionPeriodKey, TimeSpan.FromDays(14));
            options.AuditRetentionPeriod = s.GetValue(AuditRetentionPeriodKey, TimeSpan.Zero);
            options.ExternalIntegrationsDispatchingBatchSize = s.GetValue(ExternalIntegrationsDispatchingBatchSizeKey, 100);
            options.MaintenanceMode = s.GetValue(MaintenanceModeKey, false);
            options.LogPath = s.GetValue(LogsPathKey, DefaultLogLocation());
            options.LogsMode = logsMode;
            options.EnableFullTextSearchOnBodies = s.GetValue("EnableFullTextSearchOnBodies", true);

            var licensingComponentSection = configuration.GetSection("LicensingComponent");

            options.ThroughputDatabaseName = licensingComponentSection.GetValue(ThroughputSettings.DatabaseNameKey, ThroughputSettings.DefaultDatabaseName);
        }

        public PersistenceSettings CreateSettings(SettingsRootNamespace settingsRootNamespace)
        {
            var settings = new RavenPersisterSettings();
            Configure(settings);
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
    }

    class RavenPersisterSettingsValidation : IValidateOptions<RavenPersisterSettings>
    {
        public ValidateOptionsResult Validate(string name, RavenPersisterSettings options)
        {
            return options.ErrorRetentionPeriod==TimeSpan.Zero
                ? ValidateOptionsResult.Fail("ErrorRetentionPeriod must be set than 0")
                : ValidateOptionsResult.Success;
        }
    }
}