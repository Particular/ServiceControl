namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Threading.Tasks;
    using Configuration;
    using NLog;
    using NLog.Common;
    using Transports;

    public class Settings
    {
        public Settings()
        {
            TryLoadLicenseFromConfig();

            TransportType = SettingsReader.Read<string>(SettingsRootNamespace, "TransportType");

            ConnectionString = GetConnectionString();
            LogLevel = InitializeLogLevel();
            LogPath = SettingsReader.Read(SettingsRootNamespace, "LogPath", DefaultLogLocation());
            ErrorQueue = SettingsReader.Read(SettingsRootNamespace, "ErrorQueue", "error");
            HttpHostName = SettingsReader.Read<string>(SettingsRootNamespace, "HttpHostname");
            HttpPort = SettingsReader.Read<string>(SettingsRootNamespace, "HttpPort");
            EndpointName = SettingsReader.Read<string>(SettingsRootNamespace, "EndpointName");
            EndpointUptimeGracePeriod = TimeSpan.Parse(SettingsReader.Read(SettingsRootNamespace, "EndpointUptimeGracePeriod", "00:00:40"));
            MaximumConcurrencyLevel = SettingsReader.Read(SettingsRootNamespace, "MaximumConcurrencyLevel", 32);
        }

        public string EndpointName
        {
            get { return endpointName ?? ServiceName; }
            set { endpointName = value; }
        }

        public bool Portable { get; set; } = false;
        public string ServiceName { get; set; } = DEFAULT_ENDPOINT_NAME;
        public string TransportType { get; set; }
        public string ConnectionString { get; set; }
        public string ErrorQueue { get; set; }
        public string LogPath { get; set; }
        public LogLevel LogLevel { get; set; }
        public string Username { get; set; }
        public bool EnableInstallers { get; set; }
        public string HttpHostName { get; set; }
        public string HttpPort { get; set; }
        public TimeSpan EndpointUptimeGracePeriod { get; set; }
        public bool SkipQueueCreation { get; set; }
        public string RootUrl => $"http://{HttpHostName}:{HttpPort}/";
        public int MaximumConcurrencyLevel { get; set; }
        public string LicenseFileText { get; set; }

        static LogLevel InitializeLogLevel()
        {
            var defaultLevel = LogLevel.Info;

            var levelText = SettingsReader.Read<string>(SettingsRootNamespace, logLevelKey);

            if (string.IsNullOrWhiteSpace(levelText))
            {
                return defaultLevel;
            }

            try
            {
                return LogLevel.FromString(levelText);
            }
            catch
            {
                InternalLogger.Warn($"Failed to parse {logLevelKey} setting. Defaulting to {defaultLevel.Name}.");
                return defaultLevel;
            }
        }

        // SC installer always populates LogPath in app.config on installation/change/upgrade so this will only be used when
        // debugging or if the entry is removed manually. In those circumstances default to the folder containing the exe
        static string DefaultLogLocation() => Path.Combine(AppContext.BaseDirectory, ".logs");

        public Microsoft.Extensions.Logging.LogLevel ToHostLogLevel() => LogLevel switch
        {
            _ when LogLevel == LogLevel.Trace => Microsoft.Extensions.Logging.LogLevel.Trace,
            _ when LogLevel == LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
            _ when LogLevel == LogLevel.Info => Microsoft.Extensions.Logging.LogLevel.Information,
            _ when LogLevel == LogLevel.Warn => Microsoft.Extensions.Logging.LogLevel.Warning,
            _ when LogLevel == LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
            _ when LogLevel == LogLevel.Fatal => Microsoft.Extensions.Logging.LogLevel.Critical,
            _ => Microsoft.Extensions.Logging.LogLevel.None
        };

        void TryLoadLicenseFromConfig() => LicenseFileText = SettingsReader.Read<string>(SettingsRootNamespace, "LicenseText");

        public ITransportCustomization LoadTransportCustomization()
        {
            try
            {
                TransportType = TransportManifestLibrary.Find(TransportType);

                var customizationType = Type.GetType(TransportType, true);

                return (ITransportCustomization)Activator.CreateInstance(customizationType);
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load transport customization type {TransportType}.", e);
            }
        }

        static string GetConnectionString()
        {
            var settingsValue = SettingsReader.Read<string>(SettingsRootNamespace, "ConnectionString");
            if (settingsValue != null)
            {
                return settingsValue;
            }

            var connectionStringSettings = ConfigurationManager.ConnectionStrings["NServiceBus/Transport"];
            return connectionStringSettings?.ConnectionString;
        }

        string endpointName;

        internal Func<string, Dictionary<string, string>, byte[], Func<Task>, Task> OnMessage { get; set; } = (messageId, headers, body, next) => next();

        const string logLevelKey = "LogLevel";
        public const string DEFAULT_ENDPOINT_NAME = "Particular.Monitoring";
        public static readonly SettingsRootNamespace SettingsRootNamespace = new("Monitoring");
    }
}