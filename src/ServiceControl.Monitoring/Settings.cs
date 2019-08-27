namespace ServiceControl.Monitoring
{
    using System.IO;
    using System.Reflection;
    using NLog;
    using System;

    public class Settings
    {
        const string DEFAULT_ENDPOINT_NAME = "Particular.Monitoring";

        string endpointName;

        public string EndpointName
        {
            get { return endpointName ?? ServiceName; }
            set { endpointName = value; }
        }
        public string ServiceName { get; set; } = DEFAULT_ENDPOINT_NAME;
        public string TransportType { get; set; }
        public string ErrorQueue { get; set; }
        public string LogPath { get; set; }
        public LogLevel LogLevel { get; set; }
        public string Username { get; set; }
        public bool EnableInstallers { get; set; }
        public string HttpHostName { get; set; }
        public string HttpPort { get; set; }
        public TimeSpan EndpointUptimeGracePeriod { get; set; }
        public bool SkipQueueCreation { get; set; }

        internal static Settings Load(SettingsReader reader)
        {
            var settings = new Settings
            {
                TransportType = reader.Read<string>("Monitoring/TransportType"),
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
    }
}