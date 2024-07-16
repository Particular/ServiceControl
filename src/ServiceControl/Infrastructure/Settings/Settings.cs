namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using System.Runtime.Loader;
    using System.Text.Json.Serialization;
    using NLog.Common;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using ServiceControl.Configuration;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.Settings;
    using ServiceControl.Infrastructure.WebApi;
    using ServiceControl.Persistence;
    using ServiceControl.Transports;
    using JsonSerializer = System.Text.Json.JsonSerializer;

    public class Settings
    {
        public Settings(
            string transportType = null,
            string persisterType = null,
            LoggingSettings loggingSettings = null,
            bool? forwardErrorMessages = default,
            TimeSpan? errorRetentionPeriod = default
            )
        {
            LoggingSettings = loggingSettings ?? new(SettingsRootNamespace);

            // Overwrite the instance name if it is specified in ENVVAR, reg, or config file -- LEGACY SETTING NAME
            InstanceName = SettingsReader.Read(SettingsRootNamespace, "InternalQueueName", InstanceName);

            // Overwrite the instance name if it is specified in ENVVAR, reg, or config file
            InstanceName = SettingsReader.Read(SettingsRootNamespace, "InstanceName", InstanceName);

            LoadErrorIngestionSettings();

            TransportConnectionString = GetConnectionString();
            TransportType = transportType ?? SettingsReader.Read<string>(SettingsRootNamespace, "TransportType");
            PersistenceType = persisterType ?? SettingsReader.Read<string>(SettingsRootNamespace, "PersistenceType");
            AuditRetentionPeriod = GetAuditRetentionPeriod();
            ForwardErrorMessages = forwardErrorMessages ?? GetForwardErrorMessages();
            ErrorRetentionPeriod = errorRetentionPeriod ?? GetErrorRetentionPeriod();
            EventsRetentionPeriod = GetEventRetentionPeriod();

            if (AppEnvironment.RunningInContainer)
            {
                Hostname = "*";
                Port = 33333;
            }
            else
            {
                Port = SettingsReader.Read(SettingsRootNamespace, "Port", 33333);
                Hostname = SettingsReader.Read(SettingsRootNamespace, "Hostname", "localhost");
            }

            ProcessRetryBatchesFrequency = TimeSpan.FromSeconds(30);
            MaximumConcurrencyLevel = SettingsReader.Read(SettingsRootNamespace, "MaximumConcurrencyLevel", 10);
            RetryHistoryDepth = SettingsReader.Read(SettingsRootNamespace, "RetryHistoryDepth", 10);
            AllowMessageEditing = SettingsReader.Read<bool>(SettingsRootNamespace, "AllowMessageEditing");
            NotificationsFilter = SettingsReader.Read<string>(SettingsRootNamespace, "NotificationsFilter");
            RemoteInstances = GetRemoteInstances().ToArray();
            TimeToRestartErrorIngestionAfterFailure = GetTimeToRestartErrorIngestionAfterFailure();
            DisableExternalIntegrationsPublishing = SettingsReader.Read(SettingsRootNamespace, "DisableExternalIntegrationsPublishing", false);

            AssemblyLoadContextResolver = static assemblyPath => new PluginAssemblyLoadContext(assemblyPath);
        }

        [JsonIgnore]
        public Func<string, AssemblyLoadContext> AssemblyLoadContextResolver { get; set; }

        public LoggingSettings LoggingSettings { get; }

        public string NotificationsFilter { get; set; }

        public bool AllowMessageEditing { get; set; }

        //HINT: acceptance tests only
        public Func<MessageContext, bool> MessageFilter { get; set; }

        //HINT: acceptance tests only
        public string EmailDropFolder { get; set; }

        public bool ValidateConfiguration => SettingsReader.Read(SettingsRootNamespace, "ValidateConfig", true);

        public int ExternalIntegrationsDispatchingBatchSize => SettingsReader.Read(SettingsRootNamespace, "ExternalIntegrationsDispatchingBatchSize", 100);

        public bool DisableExternalIntegrationsPublishing { get; set; }

        public bool RunCleanupBundle { get; set; }

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

        string _instanceId;
        public string InstanceId
        {
            get
            {
                if (string.IsNullOrEmpty(_instanceId))
                {
                    _instanceId = InstanceIdGenerator.FromApiUrl(ApiUrl);
                }

                return _instanceId;
            }
        }

        public string StorageUrl => $"{RootUrl}storage";

        public string StagingQueue => $"{InstanceName}.staging";

        public int Port { get; private set; }

        public PersistenceSettings PersisterSpecificSettings { get; set; }

        public bool PrintMetrics => SettingsReader.Read<bool>(SettingsRootNamespace, "PrintMetrics");
        public string Hostname { get; private set; }
        public string VirtualDirectory => SettingsReader.Read(SettingsRootNamespace, "VirtualDirectory", string.Empty);

        public TimeSpan HeartbeatGracePeriod
        {
            get
            {
                try
                {
                    return TimeSpan.Parse(SettingsReader.Read(SettingsRootNamespace, "HeartbeatGracePeriod", "00:00:40"));
                }
                catch (Exception ex)
                {
                    logger.Error($"HeartbeatGracePeriod settings invalid - {ex}. Defaulting HeartbeatGracePeriod to '00:00:40'");
                    return TimeSpan.FromSeconds(40);
                }
            }
        }

        public string TransportType { get; set; }
        public string PersistenceType { get; private set; }
        public string ErrorLogQueue { get; set; }
        public string ErrorQueue { get; set; }

        public bool ForwardErrorMessages { get; set; }

        public bool IngestErrorMessages { get; set; } = true;
        public bool RunRetryProcessor { get; set; } = true;

        public TimeSpan? AuditRetentionPeriod { get; set; }

        public TimeSpan ErrorRetentionPeriod { get; }

        public TimeSpan EventsRetentionPeriod { get; }

        public string InstanceName { get; init; } = DEFAULT_INSTANCE_NAME;

        public string TransportConnectionString { get; set; }
        public TimeSpan ProcessRetryBatchesFrequency { get; set; }
        public TimeSpan TimeToRestartErrorIngestionAfterFailure { get; set; }
        public int MaximumConcurrencyLevel { get; set; }

        public int RetryHistoryDepth { get; set; }

        public RemoteInstanceSetting[] RemoteInstances { get; set; }

        public bool DisableHealthChecks { get; set; }

        public string GetConnectionString()
        {
            var settingsValue = SettingsReader.Read<string>(SettingsRootNamespace, "ConnectionString");
            if (settingsValue != null)
            {
                return settingsValue;
            }

            var connectionStringSettings = ConfigurationManager.ConnectionStrings["NServiceBus/Transport"];
            return connectionStringSettings?.ConnectionString;
        }

        public TransportSettings ToTransportSettings()
        {
            var transportSettings = new TransportSettings
            {
                EndpointName = InstanceName,
                ConnectionString = TransportConnectionString,
                MaxConcurrency = MaximumConcurrencyLevel,
                RunCustomChecks = true,
                TransportType = TransportType,
                AssemblyLoadContextResolver = AssemblyLoadContextResolver
            };
            return transportSettings;
        }

        static bool GetForwardErrorMessages()
        {
            var forwardErrorMessages = SettingsReader.Read<bool?>(SettingsRootNamespace, "ForwardErrorMessages");
            if (forwardErrorMessages.HasValue)
            {
                return forwardErrorMessages.Value;
            }

            throw new Exception("ForwardErrorMessages settings is missing, please make sure it is included.");
        }

        TimeSpan GetEventRetentionPeriod()
        {
            var valueRead = SettingsReader.Read<string>(SettingsRootNamespace, "EventRetentionPeriod");
            if (valueRead != null)
            {
                if (TimeSpan.TryParse(valueRead, out var result))
                {
                    string message;
                    if (ValidateConfiguration && result < TimeSpan.FromHours(1))
                    {
                        message = "EventRetentionPeriod settings is invalid, value should be minimum 1 hour.";
                        logger.Fatal(message);
                        throw new Exception(message);
                    }

                    if (ValidateConfiguration && result > TimeSpan.FromDays(200))
                    {
                        message = "EventRetentionPeriod settings is invalid, value should be maximum 200 days.";
                        logger.Fatal(message);
                        throw new Exception(message);
                    }

                    return result;
                }
            }

            return TimeSpan.FromDays(14);
        }

        TimeSpan GetErrorRetentionPeriod()
        {
            string message;
            var valueRead = SettingsReader.Read<string>(SettingsRootNamespace, "ErrorRetentionPeriod");
            if (valueRead == null)
            {
                message = "ErrorRetentionPeriod settings is missing, please make sure it is included.";
                logger.Fatal(message);
                throw new Exception(message);
            }

            if (TimeSpan.TryParse(valueRead, out var result))
            {
                if (ValidateConfiguration && result < TimeSpan.FromDays(5))
                {
                    message = "ErrorRetentionPeriod settings is invalid, value should be minimum 5 days.";
                    logger.Fatal(message);
                    throw new Exception(message);
                }

                if (ValidateConfiguration && result > TimeSpan.FromDays(45))
                {
                    message = "ErrorRetentionPeriod settings is invalid, value should be maximum 45 days.";
                    logger.Fatal(message);
                    throw new Exception(message);
                }
            }
            else
            {
                message = "ErrorRetentionPeriod settings is invalid, please make sure it is a TimeSpan.";
                logger.Fatal(message);
                throw new Exception(message);
            }

            return result;
        }

        TimeSpan? GetAuditRetentionPeriod()
        {
            string message;
            var valueRead = SettingsReader.Read<string>(SettingsRootNamespace, "AuditRetentionPeriod");
            if (valueRead == null)
            {
                return null;
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

        TimeSpan GetTimeToRestartErrorIngestionAfterFailure()
        {
            string message;
            var valueRead = SettingsReader.Read<string>(SettingsRootNamespace, "TimeToRestartErrorIngestionAfterFailure");
            if (valueRead == null)
            {
                return TimeSpan.FromSeconds(60);
            }

            if (TimeSpan.TryParse(valueRead, out var result))
            {
                if (ValidateConfiguration && result < TimeSpan.FromSeconds(5))
                {
                    message = "TimeToRestartErrorIngestionAfterFailure setting is invalid, value should be minimum 5 seconds.";
                    InternalLogger.Fatal(message);
                    throw new Exception(message);
                }

                if (ValidateConfiguration && result > TimeSpan.FromHours(1))
                {
                    message = "TimeToRestartErrorIngestionAfterFailure setting is invalid, value should be maximum 1 hour.";
                    InternalLogger.Fatal(message);
                    throw new Exception(message);
                }
            }
            else
            {
                message = "TimeToRestartErrorIngestionAfterFailure setting is invalid, please make sure it is a TimeSpan.";
                InternalLogger.Fatal(message);
                throw new Exception(message);
            }

            return result;
        }

        static IList<RemoteInstanceSetting> GetRemoteInstances()
        {
            var valueRead = SettingsReader.Read<string>(SettingsRootNamespace, "RemoteInstances");
            if (!string.IsNullOrEmpty(valueRead))
            {
                return ParseRemoteInstances(valueRead);
            }

            return Array.Empty<RemoteInstanceSetting>();
        }

        internal static RemoteInstanceSetting[] ParseRemoteInstances(string value) =>
            JsonSerializer.Deserialize<RemoteInstanceSetting[]>(value, SerializerOptions.Default) ?? [];

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

        void LoadErrorIngestionSettings()
        {
            var serviceBusRootNamespace = new SettingsRootNamespace("ServiceBus");
            ErrorQueue = SettingsReader.Read(serviceBusRootNamespace, "ErrorQueue", "error");

            if (string.IsNullOrEmpty(ErrorQueue))
            {
                throw new Exception("ServiceBus/ErrorQueue requires a value to start the instance");
            }

            IngestErrorMessages = SettingsReader.Read(SettingsRootNamespace, "IngestErrorMessages", true);

            if (!IngestErrorMessages)
            {
                logger.Info("Error ingestion disabled.");
            }

            ErrorLogQueue = SettingsReader.Read<string>(serviceBusRootNamespace, "ErrorLogQueue", null);

            if (ErrorLogQueue == null)
            {
                logger.Info("No settings found for error log queue to import, default name will be used");
                ErrorLogQueue = Subscope(ErrorQueue);
            }
        }

        // logger is intentionally not static to prevent it from being initialized before LoggingConfigurator.ConfigureLogging has been called
        readonly ILog logger = LogManager.GetLogger(typeof(Settings));

        public const string DEFAULT_INSTANCE_NAME = "Particular.ServiceControl";
        public static readonly SettingsRootNamespace SettingsRootNamespace = new("ServiceControl");
    }
}