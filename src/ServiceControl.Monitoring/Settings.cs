namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;
    using Configuration;
    using NLog;
    using Transports;

    public class Settings
    {
        public Settings()
        {
            TryLoadLicenseFromConfig();

            TransportType = SettingsReader.Read<string>(SettingsRootNamespace, "TransportType");

            ConnectionString = GetConnectionString();
            LogLevel = LoggingConfigurator.InitializeLevel();
            LogPath = SettingsReader.Read(SettingsRootNamespace, "LogPath", DefaultLogLocation());
            ErrorQueue = SettingsReader.Read(SettingsRootNamespace, "ErrorQueue", "error");
            HttpHostName = SettingsReader.Read<string>(SettingsRootNamespace, "HttpHostname");
            HttpPort = SettingsReader.Read<string>(SettingsRootNamespace, "HttpPort");
            EndpointName = SettingsReader.Read<string>(SettingsRootNamespace, "EndpointName");
            EndpointUptimeGracePeriod = TimeSpan.Parse(SettingsReader.Read<string>(SettingsRootNamespace, "EndpointUptimeGracePeriod", "00:00:40"));
            MaximumConcurrencyLevel = SettingsReader.Read<int>(SettingsRootNamespace, "MaximumConcurrencyLevel", 32);
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

        public const string DEFAULT_ENDPOINT_NAME = "Particular.Monitoring";
        public static readonly SettingsRootNamespace SettingsRootNamespace = new("Monitoring");
    }
}