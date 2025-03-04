namespace ServiceControl.Audit.Infrastructure.Settings
{
    using System;
    using System.Configuration;
    using System.Runtime.Loader;
    using System.Text.Json.Serialization;
    using Configuration;
    using NLog.Common;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using ServiceControl.Infrastructure;
    using Transports;

    public class Settings
    {
        public Settings(string transportType = null, string persisterType = null, LoggingSettings loggingSettings = null)
        {
            LoggingSettings = loggingSettings ?? new(SettingsRootNamespace);

            // Overwrite the instance name if it is specified in ENVVAR, reg, or config file -- LEGACY SETTING NAME
            InstanceName = SettingsReader.Read(SettingsRootNamespace, "InternalQueueName", InstanceName);

            // Overwrite the instance name if it is specified in ENVVAR, reg, or config file
            InstanceName = SettingsReader.Read(SettingsRootNamespace, "InstanceName", InstanceName);

            TransportType = transportType ?? SettingsReader.Read<string>(SettingsRootNamespace, "TransportType");

            PersistenceType = persisterType ?? SettingsReader.Read<string>(SettingsRootNamespace, "PersistenceType");

            TransportConnectionString = GetConnectionString();

            LoadAuditQueueInformation();

            ForwardAuditMessages = GetForwardAuditMessages();
            AuditRetentionPeriod = GetAuditRetentionPeriod();

            if (AppEnvironment.RunningInContainer)
            {
                Hostname = "*";
                Port = 44444;
            }
            else
            {
                Hostname = SettingsReader.Read(SettingsRootNamespace, "Hostname", "localhost");
                Port = SettingsReader.Read(SettingsRootNamespace, "Port", 44444);
            };

            MaximumConcurrencyLevel = SettingsReader.Read<int?>(SettingsRootNamespace, "MaximumConcurrencyLevel");
            ServiceControlQueueAddress = SettingsReader.Read<string>(SettingsRootNamespace, "ServiceControlQueueAddress");
            TimeToRestartAuditIngestionAfterFailure = GetTimeToRestartAuditIngestionAfterFailure();
            EnableFullTextSearchOnBodies = SettingsReader.Read(SettingsRootNamespace, "EnableFullTextSearchOnBodies", true);
            ShutdownTimeout = SettingsReader.Read(SettingsRootNamespace, "ShutdownTimeout", ShutdownTimeout);

            AssemblyLoadContextResolver = static assemblyPath => new PluginAssemblyLoadContext(assemblyPath);
        }

        void LoadAuditQueueInformation()
        {
            var serviceBusRootNamespace = new SettingsRootNamespace("ServiceBus");
            AuditQueue = SettingsReader.Read(serviceBusRootNamespace, "AuditQueue", "audit");

            if (string.IsNullOrEmpty(AuditQueue))
            {
                throw new Exception("ServiceBus/AuditQueue value is required to start the instance");
            }

            IngestAuditMessages = SettingsReader.Read(new SettingsRootNamespace("ServiceControl"), "IngestAuditMessages", true);

            if (IngestAuditMessages == false)
            {
                logger.Info("Audit ingestion disabled.");
            }

            AuditLogQueue = SettingsReader.Read<string>(serviceBusRootNamespace, "AuditLogQueue", null);

            if (AuditLogQueue == null)
            {
                logger.Info("No settings found for audit log queue to import, default name will be used");
                AuditLogQueue = Subscope(AuditQueue);
            }
        }

        [JsonIgnore]
        public Func<string, AssemblyLoadContext> AssemblyLoadContextResolver { get; set; }

        public LoggingSettings LoggingSettings { get; }

        //HINT: acceptance tests only
        public Func<MessageContext, bool> MessageFilter { get; set; }

        public bool ValidateConfiguration => SettingsReader.Read(SettingsRootNamespace, "ValidateConfig", true);

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

        public bool PrintMetrics => SettingsReader.Read<bool>(SettingsRootNamespace, "PrintMetrics");
        public string OtlpEndpointUrl { get; set; } = SettingsReader.Read<string>(SettingsRootNamespace, nameof(OtlpEndpointUrl));
        public string Hostname { get; private set; }
        public string VirtualDirectory => SettingsReader.Read(SettingsRootNamespace, "VirtualDirectory", string.Empty);

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
                    logger.Error($"MaxBodySizeToStore settings is invalid, {1} is the minimum value. Defaulting to {MaxBodySizeToStoreDefault}");
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

        TimeSpan GetTimeToRestartAuditIngestionAfterFailure()
        {
            string message;
            var valueRead = SettingsReader.Read<string>(SettingsRootNamespace, "TimeToRestartAuditIngestionAfterFailure");
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

        static bool GetForwardAuditMessages()
        {
            var forwardAuditMessages = SettingsReader.Read<bool?>(SettingsRootNamespace, "ForwardAuditMessages");
            if (forwardAuditMessages.HasValue)
            {
                return forwardAuditMessages.Value;
            }

            return false;
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

        TimeSpan GetAuditRetentionPeriod()
        {
            string message;
            var valueRead = SettingsReader.Read<string>(SettingsRootNamespace, "AuditRetentionPeriod");
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
        readonly ILog logger = LogManager.GetLogger(typeof(Settings));

        int maxBodySizeToStore = SettingsReader.Read(SettingsRootNamespace, "MaxBodySizeToStore", MaxBodySizeToStoreDefault);

        public const string DEFAULT_INSTANCE_NAME = "Particular.ServiceControl.Audit";
        public static readonly SettingsRootNamespace SettingsRootNamespace = new("ServiceControl.Audit");

        const int MaxBodySizeToStoreDefault = 102400; //100 kb
    }
}