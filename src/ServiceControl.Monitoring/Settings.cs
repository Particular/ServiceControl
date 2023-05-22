namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;
    using NLog;
    using ServiceControl.Monitoring.Infrastructure.Settings;
    using Transports;

    public class Settings
    {
        public Settings()
        {
            TryLoadLicenseFromConfig();

            TransportType = SettingsReader<string>.Read("TransportType");

            ConnectionString = GetConnectionString();
            LogLevel = LoggingConfigurator.InitializeLevel();
            LogPath = SettingsReader<string>.Read("LogPath", DefaultLogLocation());
            ErrorQueue = SettingsReader<string>.Read("ErrorQueue", "error");
            HttpHostName = SettingsReader<string>.Read("HttpHostname");
            HttpPort = SettingsReader<string>.Read("HttpPort");
            EndpointName = SettingsReader<string>.Read("EndpointName");
            EndpointUptimeGracePeriod = TimeSpan.Parse(SettingsReader<string>.Read("EndpointUptimeGracePeriod", "00:00:40"));
            MaximumConcurrencyLevel = SettingsReader<int>.Read("MaximumConcurrencyLevel", 32);
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
        public bool ExposeApi { get; set; } = true;

        // SC installer always populates LogPath in app.config on installation/change/upgrade so this will only be used when
        // debugging or if the entry is removed manually. In those circumstances default to the folder containing the exe
        internal static string DefaultLogLocation()
        {
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            return Path.GetDirectoryName(assemblyLocation);
        }

        void TryLoadLicenseFromConfig()
        {
            LicenseFileText = SettingsReader<string>.Read("LicenseText");
        }

        public TransportCustomization LoadTransportCustomization()
        {
            try
            {
                TransportType = TransportManifestLibrary.Find(TransportType);

                var customizationType = Type.GetType(TransportType, true);

                return (TransportCustomization)Activator.CreateInstance(customizationType);
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load transport customization type {TransportType}.", e);
            }
        }

        static string GetConnectionString()
        {
            var settingsValue = SettingsReader<string>.Read("ConnectionString");
            if (settingsValue != null)
            {
                return settingsValue;
            }

            var connectionStringSettings = ConfigurationManager.ConnectionStrings["NServiceBus/Transport"];
            return connectionStringSettings?.ConnectionString;
        }

        string endpointName;

        public Func<string, Dictionary<string, string>, byte[], Func<Task>, Task> OnMessage { get; set; } = (messageId, headers, body, next) => next();

        public const string DEFAULT_ENDPOINT_NAME = "Particular.Monitoring";
    }
}