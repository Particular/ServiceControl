namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Runtime.Loader;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Configuration;
    using ServiceControl.Infrastructure;
    using Transports;

    public class Settings
    {
        public Settings(LoggingSettings loggingSettings = null)
        {
            LoggingSettings = loggingSettings ?? new(SettingsRootNamespace);

            TransportType = SettingsReader.Read<string>(SettingsRootNamespace, "TransportType");

            ConnectionString = GetConnectionString();
            ErrorQueue = SettingsReader.Read(SettingsRootNamespace, "ErrorQueue", "error");

            if (AppEnvironment.RunningInContainer)
            {
                HttpHostName = "*";
                HttpPort = "33633";

            }
            else
            {
                HttpHostName = SettingsReader.Read<string>(SettingsRootNamespace, "HttpHostname");
                HttpPort = SettingsReader.Read<string>(SettingsRootNamespace, "HttpPort");
            }
            EndpointName = SettingsReader.Read<string>(SettingsRootNamespace, "EndpointName");
            EndpointUptimeGracePeriod = TimeSpan.Parse(SettingsReader.Read(SettingsRootNamespace, "EndpointUptimeGracePeriod", "00:00:40"));
            MaximumConcurrencyLevel = SettingsReader.Read(SettingsRootNamespace, "MaximumConcurrencyLevel", 32);
            ServiceControlThroughputDataQueue = SettingsReader.Read(SettingsRootNamespace, "ServiceControlThroughputDataQueue", "ServiceControl.ThroughputData");

            AssemblyLoadContextResolver = static assemblyPath => new PluginAssemblyLoadContext(assemblyPath);
        }

        public string EndpointName
        {
            get => endpointName ?? ServiceName;
            set => endpointName = value;
        }

        [JsonIgnore]
        public Func<string, AssemblyLoadContext> AssemblyLoadContextResolver { get; set; }

        public LoggingSettings LoggingSettings { get; }

        public string ServiceName { get; set; } = DEFAULT_ENDPOINT_NAME;

        public string TransportType { get; set; }

        public string ConnectionString { get; set; }

        public string ErrorQueue { get; set; }

        public string HttpHostName { get; set; }

        public string HttpPort { get; set; }

        public TimeSpan EndpointUptimeGracePeriod { get; set; }

        public string RootUrl => $"http://{HttpHostName}:{HttpPort}/";

        public int MaximumConcurrencyLevel { get; set; }

        public string ServiceControlThroughputDataQueue { get; set; }

        public TransportSettings ToTransportSettings()
        {
            var transportSettings = new TransportSettings
            {
                ConnectionString = ConnectionString,
                EndpointName = EndpointName,
                ErrorQueue = ErrorQueue,
                MaxConcurrency = MaximumConcurrencyLevel,
                AssemblyLoadContextResolver = AssemblyLoadContextResolver,
                TransportType = TransportType
            };
            return transportSettings;
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