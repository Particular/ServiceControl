namespace ServiceControl.Audit.Infrastructure.Settings
{
    using System;
    using System.Runtime.Loader;
    using System.Text.Json.Serialization;
    using Configuration;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using NLog.Common;
    using NServiceBus.Transport;
    using ServiceControl.Infrastructure;
    using Transports;
    using ConfigurationManager = System.Configuration.ConfigurationManager;

    public class Settings
    {
        // TODO: All kinds of validation happening but validation and deserialization should be split
        public Settings(
            IConfiguration configuration,
            string transportType = null,
            string persisterType = null,
            LoggingSettings loggingSettings = null
        )
        {
            IConfiguration serviceBusSection = configuration.GetSection(SectionNameServiceBus);
            IConfiguration serviceControlAuditsection = configuration.GetSection(SectionName);
            IConfiguration serviceControlSection = configuration.GetSection(SectionNameServiceControl);

            LoggingSettings = loggingSettings; // TODO: ?? new();

            // Overwrite the instance name if it is specified in ENVVAR, reg, or config file -- LEGACY SETTING NAME
            InstanceName = serviceControlAuditsection.GetValue("InternalQueueName", InstanceName);

            // Overwrite the instance name if it is specified in ENVVAR, reg, or config file
            InstanceName = serviceControlAuditsection.GetValue("InstanceName", InstanceName);

            TransportType = transportType ?? serviceControlAuditsection.GetValue<string>("TransportType");

            PersistenceType = persisterType ?? serviceControlAuditsection.GetValue<string>("PersistenceType");

            TransportConnectionString = GetConnectionString(serviceControlAuditsection);

            LoadAuditQueueInformation(
                serviceControlAuditsection,
                serviceBusSection,
                serviceControlSection
            ); //TODO: Ugly, extract to its own settings class

            ForwardAuditMessages = GetForwardAuditMessages(serviceControlAuditsection);
            AuditRetentionPeriod = GetAuditRetentionPeriod(serviceControlAuditsection);

            if (AppEnvironment.RunningInContainer)
            {
                Hostname = "*";
                Port = 44444;
            }
            else
            {
                Hostname = serviceControlAuditsection.GetValue("Hostname", "localhost");
                Port = serviceControlAuditsection.GetValue("Port", 44444);
            }

            MaximumConcurrencyLevel = serviceControlAuditsection.GetValue<int?>("MaximumConcurrencyLevel");
            ServiceControlQueueAddress = serviceControlAuditsection.GetValue<string>("ServiceControlQueueAddress");
            TimeToRestartAuditIngestionAfterFailure = GetTimeToRestartAuditIngestionAfterFailure(serviceControlAuditsection);
            EnableFullTextSearchOnBodies = serviceControlAuditsection.GetValue("EnableFullTextSearchOnBodies", true);
            ShutdownTimeout = serviceControlAuditsection.GetValue("ShutdownTimeout", ShutdownTimeout);

            AssemblyLoadContextResolver = static assemblyPath => new PluginAssemblyLoadContext(assemblyPath);

            PrintMetrics = serviceControlAuditsection.GetValue<bool>("PrintMetrics");
            OtlpEndpointUrl = serviceControlAuditsection.GetValue<string>(nameof(OtlpEndpointUrl));
            VirtualDirectory = serviceControlAuditsection.GetValue("VirtualDirectory", string.Empty);
            maxBodySizeToStore = serviceControlAuditsection.GetValue("MaxBodySizeToStore", MaxBodySizeToStoreDefault);
            ValidateConfiguration = serviceControlAuditsection.GetValue("ValidateConfig", true);

            Name = serviceControlAuditsection.GetValue("Name", "ServiceControl.Audit");
            Description = serviceControlAuditsection.GetValue("Description", "The audit backend for the Particular Service Platform");
            MaintenanceMode = serviceControlAuditsection.GetValue("MaintenanceMode", false);
        }

        void LoadAuditQueueInformation(
            IConfiguration serviceControlAuditsection,
            IConfiguration serviceBusSection,
            IConfiguration serviceControlSection
        )
        {
            AuditQueue = serviceBusSection.GetValue("AuditQueue", "audit");

            if (string.IsNullOrEmpty(AuditQueue))
            {
                throw new Exception("ServiceBus/AuditQueue value is required to start the instance");
            }

            IngestAuditMessages = serviceControlAuditsection.GetValue<bool?>("IngestAuditMessages")
                                  // Backwards compatibility
                                  // TODO: How far does this go back?
                                  ?? serviceControlSection.GetValue("IngestAuditMessages", true);

            if (IngestAuditMessages == false)
            {
                logger.LogInformation("Audit ingestion disabled");
            }

            AuditLogQueue = serviceBusSection.GetValue<string>("AuditLogQueue", null);

            if (AuditLogQueue == null)
            {
                logger.LogInformation("No settings found for audit log queue to import, default name will be used");
                AuditLogQueue = Subscope(AuditQueue);
            }
        }

        [JsonIgnore] public Func<string, AssemblyLoadContext> AssemblyLoadContextResolver { get; set; }

        public LoggingSettings LoggingSettings { get; }

        //HINT: acceptance tests only
        public Func<MessageContext, bool> MessageFilter { get; set; }

        public bool ValidateConfiguration { get; }

        public string RootUrl
        {
            get
            {
                var suffix = string.Empty;

                if (!string.IsNullOrEmpty(VirtualDirectory))
                {
                    suffix = $"{VirtualDirectory}/";
                }

                return $"http://{Hostname}:{Port}/{suffix}";
            }
        }

        public string ApiUrl => $"{RootUrl}api";

        public int Port { get; set; }

        public bool PrintMetrics { get; set; }
        public string OtlpEndpointUrl { get; set; }
        public string Hostname { get; private set; }
        public string VirtualDirectory { get; set; }

        public string TransportType { get; private set; }

        public string PersistenceType { get; private set; }

        public string AuditQueue { get; set; }

        public bool ForwardAuditMessages { get; set; }

        public bool IngestAuditMessages { get; set; } = true;

        public string AuditLogQueue { get; set; }

        public TimeSpan AuditRetentionPeriod { get; }

        public int MaxBodySizeToStore
        {
            get
            {
                if (maxBodySizeToStore <= 0)
                {
                    logger.LogError("MaxBodySizeToStore settings is invalid, 1 is the minimum value. Defaulting to {MaxBodySizeToStoreDefault}", MaxBodySizeToStoreDefault);
                    return MaxBodySizeToStoreDefault;
                }

                return maxBodySizeToStore;
            }
            set => maxBodySizeToStore = value;
        }

        public string InstanceName { get; init; } = DEFAULT_INSTANCE_NAME;

        public string TransportConnectionString { get; set; }
        public int? MaximumConcurrencyLevel { get; set; }
        public string ServiceControlQueueAddress { get; set; }

        public TimeSpan TimeToRestartAuditIngestionAfterFailure { get; set; }

        public bool EnableFullTextSearchOnBodies { get; set; }

        // The default value is set to the maximum allowed time by the most
        // restrictive hosting platform, which is Linux containers. Linux
        // containers allow for a maximum of 10 seconds. We set it to 5 to
        // allow for cancellation and logging to take place
        public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);

        public TransportSettings ToTransportSettings()
        {
            var transportSettings = new TransportSettings
            {
                EndpointName = InstanceName,
                ConnectionString = TransportConnectionString,
                MaxConcurrency = MaximumConcurrencyLevel,
                TransportType = TransportType,
                AssemblyLoadContextResolver = AssemblyLoadContextResolver
            };
            return transportSettings;
        }

        TimeSpan GetTimeToRestartAuditIngestionAfterFailure(IConfiguration section)
        {
            string message;
            var valueRead = section.GetValue<string>("TimeToRestartAuditIngestionAfterFailure");
            if (valueRead == null)
            {
                return TimeSpan.FromSeconds(60);
            }

            if (TimeSpan.TryParse(valueRead, out var result))
            {
                if (ValidateConfiguration && result < TimeSpan.FromSeconds(5))
                {
                    message = $"{nameof(TimeToRestartAuditIngestionAfterFailure)} setting is invalid, value should be minimum 5 seconds.";
                    InternalLogger.Fatal(message);
                    throw new Exception(message);
                }

                if (ValidateConfiguration && result > TimeSpan.FromHours(1))
                {
                    message = $"{nameof(TimeToRestartAuditIngestionAfterFailure)} setting is invalid, value should be maximum 1 hour.";
                    InternalLogger.Fatal(message);
                    throw new Exception(message);
                }
            }
            else
            {
                message = $"{nameof(TimeToRestartAuditIngestionAfterFailure)} setting is invalid, please make sure it is a TimeSpan.";
                InternalLogger.Fatal(message);
                throw new Exception(message);
            }

            return result;
        }

        static bool GetForwardAuditMessages(IConfiguration serviceControlAuditsection)
        {
            var forwardAuditMessages = serviceControlAuditsection.GetValue<bool?>("ForwardAuditMessages");
            if (forwardAuditMessages.HasValue)
            {
                return forwardAuditMessages.Value;
            }

            return false;
        }

        static string GetConnectionString(IConfiguration serviceControlAuditsection)
        {
            var settingsValue = serviceControlAuditsection.GetValue<string>("ConnectionString");
            if (settingsValue != null)
            {
                return settingsValue;
            }

            var connectionStringSettings = ConfigurationManager.ConnectionStrings["NServiceBus/Transport"];
            return connectionStringSettings?.ConnectionString;
        }

        TimeSpan GetAuditRetentionPeriod(IConfiguration serviceControlAuditsection)
        {
            string message;
            var valueRead = serviceControlAuditsection.GetValue<string>("AuditRetentionPeriod");
            if (valueRead == null)
            {
                // SCMU actually defaults to 7 days, as does Dockerfile, but a change to same-up everything should be done in a major
                return TimeSpan.FromDays(30);
            }

            if (TimeSpan.TryParse(valueRead, out var result))
            {
                if (ValidateConfiguration && result < TimeSpan.FromHours(1))
                {
                    message = "AuditRetentionPeriod settings is invalid, value should be minimum 1 hour.";
                    InternalLogger.Fatal(message);
                    throw new Exception(message);
                }

                if (ValidateConfiguration && result > TimeSpan.FromDays(365))
                {
                    message = "AuditRetentionPeriod settings is invalid, value should be maximum 365 days.";
                    InternalLogger.Fatal(message);
                    throw new Exception(message);
                }
            }
            else
            {
                message = "AuditRetentionPeriod settings is invalid, please make sure it is a TimeSpan.";
                //TODO: should these InternalLoggers (NLog) be replaced?
                InternalLogger.Fatal(message);
                throw new Exception(message);
            }

            return result;
        }

        static string Subscope(string address)
        {
            var atIndex = address.IndexOf("@", StringComparison.InvariantCulture);

            if (atIndex <= -1)
            {
                return $"{address}.log";
            }

            var queue = address.Substring(0, atIndex);
            var machine = address.Substring(atIndex + 1);
            return $"{queue}.log@{machine}";
        }

        // logger is intentionally not static to prevent it from being initialized before LoggingConfigurator.ConfigureLogging has been called
        readonly ILogger logger = LoggerUtil.CreateStaticLogger<Settings>();

        int maxBodySizeToStore;

        public const string DEFAULT_INSTANCE_NAME = "Particular.ServiceControl.Audit";
        public const string SectionName = "ServiceControl.Audit";
        public const string SectionNameServiceBus = "ServiceBus";
        public const string SectionNameServiceControl = "ServiceControl";

        const int MaxBodySizeToStoreDefault = 102400; //100 kb

        public string Name { get; }
        public string Description { get; }

        public bool MaintenanceMode { get; }
    }
}