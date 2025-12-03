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
        public Settings(LoggingSettings loggingSettings = null, string transportType = null)
        {
            LoggingSettings = loggingSettings ?? new(SettingsRootNamespace);

            OpenIdConnectSettings = new OpenIdConnectSettings(SettingsRootNamespace, ValidateConfiguration, requireServicePulseSettings: false);
            ForwardedHeadersSettings = new ForwardedHeadersSettings(SettingsRootNamespace);
            HttpsSettings = new HttpsSettings(SettingsRootNamespace);
            CorsSettings = new CorsSettings(SettingsRootNamespace);

            // Overwrite the instance name if it is specified in ENVVAR, reg, or config file
            InstanceName = SettingsReader.Read(SettingsRootNamespace, "InstanceName", InstanceName);

            TransportType = SettingsReader.Read(SettingsRootNamespace, "TransportType", transportType);

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

            EndpointUptimeGracePeriod = TimeSpan.Parse(SettingsReader.Read(SettingsRootNamespace, "EndpointUptimeGracePeriod", "00:00:40"));
            MaximumConcurrencyLevel = SettingsReader.Read<int?>(SettingsRootNamespace, "MaximumConcurrencyLevel");
            ServiceControlThroughputDataQueue = SettingsReader.Read(SettingsRootNamespace, "ServiceControlThroughputDataQueue", "ServiceControl.ThroughputData");
            ShutdownTimeout = SettingsReader.Read(SettingsRootNamespace, "ShutdownTimeout", ShutdownTimeout);

            AssemblyLoadContextResolver = static assemblyPath => new PluginAssemblyLoadContext(assemblyPath);
        }

        [JsonIgnore]
        public Func<string, AssemblyLoadContext> AssemblyLoadContextResolver { get; set; }

        public LoggingSettings LoggingSettings { get; }

        public OpenIdConnectSettings OpenIdConnectSettings { get; }

        public ForwardedHeadersSettings ForwardedHeadersSettings { get; }

        public HttpsSettings HttpsSettings { get; }

        public CorsSettings CorsSettings { get; }

        public string InstanceName { get; init; } = DEFAULT_INSTANCE_NAME;

        public string TransportType { get; set; }

        public string ConnectionString { get; set; }

        public string ErrorQueue { get; set; }

        public string HttpHostName { get; set; }

        public string HttpPort { get; set; }

        public TimeSpan EndpointUptimeGracePeriod { get; set; }

        public string RootUrl => $"{(HttpsSettings.Enabled ? "https" : "http")}://{HttpHostName}:{HttpPort}/";

        public int? MaximumConcurrencyLevel { get; set; }

        public string ServiceControlThroughputDataQueue { get; set; }

        public bool ValidateConfiguration => SettingsReader.Read(SettingsRootNamespace, "ValidateConfig", true);

        // The default value is set to the maximum allowed time by the most
        // restrictive hosting platform, which is Linux containers. Linux
        // containers allow for a maximum of 10 seconds. We set it to 5 to
        // allow for cancellation and logging to take place
        public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);

        public TransportSettings ToTransportSettings()
        {
            var transportSettings = new TransportSettings
            {
                ConnectionString = ConnectionString,
                EndpointName = InstanceName,
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

        internal Func<string, Dictionary<string, string>, byte[], Func<Task>, Task> OnMessage { get; set; } = (messageId, headers, body, next) => next();

        public const string DEFAULT_INSTANCE_NAME = "Particular.Monitoring";
        public static readonly SettingsRootNamespace SettingsRootNamespace = new("Monitoring");
    }
}