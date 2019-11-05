namespace ServiceControl.Monitoring
{
    using System;
    using System.Configuration;
    using System.IO;
    using System.Reflection;
    using NLog;
    using Transports;

    public class Settings
    {
        public string EndpointName
        {
            get { return endpointName ?? ServiceName; }
            set { endpointName = value; }
        }

        public string ServiceName { get; set; } = DEFAULT_ENDPOINT_NAME;
        public string TransportCustomizationType { get; set; }
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

        internal static Settings Load(SettingsReader reader)
        {
            var settings = new Settings
            {
                TransportCustomizationType = reader.Read<string>("Monitoring/TransportType"),
                ConnectionString = GetConnectionString(reader),
                LogLevel = MonitorLogs.InitializeLevel(reader),
                LogPath = reader.Read("Monitoring/LogPath", DefaultLogLocation()),
                ErrorQueue = reader.Read("Monitoring/ErrorQueue", "error"),
                HttpHostName = reader.Read<string>("Monitoring/HttpHostname"),
                HttpPort = reader.Read<string>("Monitoring/HttpPort"),
                EndpointName = reader.Read<string>("Monitoring/EndpointName"),
                EndpointUptimeGracePeriod = TimeSpan.Parse(reader.Read("Monitoring/EndpointUptimeGracePeriod", "00:00:40"))
            };
            return settings;
        }

        // SC installer always populates LogPath in app.config on installation/change/upgrade so this will only be used when
        // debugging or if the entry is removed manually. In those circumstances default to the folder containing the exe
        internal static string DefaultLogLocation()
        {
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            return Path.GetDirectoryName(assemblyLocation);
        }

        public TransportCustomization LoadTransportCustomization()
        {
            try
            {
                var customizationType = Type.GetType(TransportCustomizationType, true);
                return (TransportCustomization)Activator.CreateInstance(customizationType);
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load transport customization type {TransportCustomizationType}.", e);
            }
        }

        static string GetConnectionString(SettingsReader reader)
        {
            var settingsValue = reader.Read<string>("ConnectionString");
            if (settingsValue != null)
            {
                return settingsValue;
            }

            var connectionStringSettings = ConfigurationManager.ConnectionStrings["NServiceBus/Transport"];
            return connectionStringSettings?.ConnectionString;
        }

        string endpointName;
        const string DEFAULT_ENDPOINT_NAME = "Particular.Monitoring";
    }
}