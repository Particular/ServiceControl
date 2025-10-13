namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Loader;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Configuration;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using ServiceControl.Infrastructure;
    using Transports;
    using ConfigurationManager = System.Configuration.ConfigurationManager;

    public class Settings
    {
        public Settings() { }

        public Settings(ILogger<Settings> logger, IConfigurationSection section, LoggingSettings loggingSettings, string transportType = null)
        {
            LoggingSettings = loggingSettings; // TODO: Previously had a COALESCE

            // Overwrite the instance name if it is specified in ENVVAR, reg, or config file
            InstanceName = section.GetValue("InstanceName", DEFAULT_INSTANCE_NAME);

            TransportType = section.GetValue("TransportType", transportType);

            ConnectionString = GetConnectionString(logger, section);
            ErrorQueue = section.GetValue("ErrorQueue", "error");

            if (AppEnvironment.RunningInContainer)
            {
                HttpHostName = "*";
                HttpPort = "33633";
            }
            else
            {
                HttpHostName = section.GetValue<string>("HttpHostname");
                HttpPort = section.GetValue<string>("HttpPort");
            }

            EndpointUptimeGracePeriod = section.GetValue("EndpointUptimeGracePeriod", TimeSpan.FromSeconds(40));
            MaximumConcurrencyLevel = section.GetValue<int?>("MaximumConcurrencyLevel");
            ServiceControlThroughputDataQueue = section.GetValue("ServiceControlThroughputDataQueue", "ServiceControl.ThroughputData");
            ShutdownTimeout = section.GetValue("ShutdownTimeout", TimeSpan.FromSeconds(5));

            AssemblyLoadContextResolver = static assemblyPath => new PluginAssemblyLoadContext(assemblyPath);
        }


        [JsonIgnore] public Func<string, AssemblyLoadContext> AssemblyLoadContextResolver { get; set; }

        public LoggingSettings LoggingSettings { get; }

        public string InstanceName { get; init; } = DEFAULT_INSTANCE_NAME;

        public string TransportType { get; set; }

        public string ConnectionString { get; set; }

        public string ErrorQueue { get; set; }

        public string HttpHostName { get; set; }

        public string HttpPort { get; set; }

        public TimeSpan EndpointUptimeGracePeriod { get; set; }

        public string RootUrl => $"http://{HttpHostName}:{HttpPort}/";

        public int? MaximumConcurrencyLevel { get; set; }

        public string ServiceControlThroughputDataQueue { get; set; }

        // The default value is set to the maximum allowed time by the most
        // restrictive hosting platform, which is Linux containers. Linux
        // containers allow for a maximum of 10 seconds. We set it to 5 to
        // allow for cancellation and logging to take place
        public TimeSpan ShutdownTimeout { get; set; }

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

        static string GetConnectionString(ILogger logger, IConfigurationSection section)
        {
            var settingsValue = section.GetValue<string>("ConnectionString"); // Intentionally NOT using .GetConnectionString() for legacy reasons
            if (settingsValue != null)
            {
                return settingsValue;
            }

            var connectionStringSettings = ConfigurationManager.ConnectionStrings["NServiceBus/Transport"];
            if (connectionStringSettings != null)
            {
                logger.LogWarning($"Connection string resolved from legacy `NServiceBus/Transport` connection string. Migrate to `{SectionName}/{nameof(ConnectionString)}`");
            }
            return connectionStringSettings?.ConnectionString;
        }

        internal Func<string, Dictionary<string, string>, byte[], Func<Task>, Task> OnMessage { get; set; } = (messageId, headers, body, next) => next();

        public const string DEFAULT_INSTANCE_NAME = "Particular.Monitoring";
        public const string SectionName = "Monitoring";

    }
}