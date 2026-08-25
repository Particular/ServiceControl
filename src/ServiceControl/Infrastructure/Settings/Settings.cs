namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using System.Runtime.Loader;
    using System.Text.Json.Serialization;
    using Microsoft.Extensions.Logging;
    using NLog.Common;
    using NServiceBus.Transport;
    using Particular.ServiceControl;
    using ServiceControl.Configuration;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.Ingestion;
    using ServiceControl.Infrastructure.Settings;
    using ServiceControl.Infrastructure.WebApi;
    using ServiceControl.Persistence;
    using ServiceControl.Transports;
    using ServicePulse;
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

            OpenIdConnectSettings = new OpenIdConnectSettings(SettingsRootNamespace, ValidateConfiguration);
            ForwardedHeadersSettings = new ForwardedHeadersSettings(SettingsRootNamespace);
            HttpsSettings = new HttpsSettings(SettingsRootNamespace);
            CorsSettings = new CorsSettings(SettingsRootNamespace);

            // Overwrite the instance name if it is specified in ENVVAR, reg, or config file -- LEGACY SETTING NAME
            InstanceName = SettingsReader.Read(SettingsRootNamespace, "InternalQueueName", InstanceName);

            // Overwrite the instance name if it is specified in ENVVAR, reg, or config file
            InstanceName = SettingsReader.Read(SettingsRootNamespace, "InstanceName", InstanceName);

            LoadErrorIngestionSettings();
            LoadAuditIngestionSettings();

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
            MaximumConcurrencyLevel = SettingsReader.Read<int?>(SettingsRootNamespace, "MaximumConcurrencyLevel");
            RetryHistoryDepth = SettingsReader.Read(SettingsRootNamespace, "RetryHistoryDepth", 10);
            AllowMessageEditing = SettingsReader.Read<bool>(SettingsRootNamespace, "AllowMessageEditing");
            EnableIntegratedServicePulse = SettingsReader.Read(SettingsRootNamespace, "EnableIntegratedServicePulse", false);
            if (EnableIntegratedServicePulse)
            {
                ServicePulseSettings = ServicePulseSettings.GetFromEnvironmentVariables() with
                {
                    ServiceControlUrl = "/api/",
                    IsIntegrated = true
                };
            }
            NotificationsFilter = SettingsReader.Read<string>(SettingsRootNamespace, "NotificationsFilter");
            RemoteInstances = GetRemoteInstances().ToArray();
            TimeToRestartErrorIngestionAfterFailure = GetTimeToRestartErrorIngestionAfterFailure();
            HeartbeatGracePeriod = GetHeartbeatGracePeriod();
            ErrorIngestionBatchSize = IngestionSettingsReader.ReadBatchSize(SettingsRootNamespace, nameof(ErrorIngestionBatchSize), ValidateConfiguration);
            ErrorIngestionMaxParallelWriters = IngestionSettingsReader.ReadMaxParallelWriters(SettingsRootNamespace, nameof(ErrorIngestionMaxParallelWriters), ValidateConfiguration);
            ErrorIngestionBatchTimeout = IngestionSettingsReader.ReadBatchTimeout(SettingsRootNamespace, nameof(ErrorIngestionBatchTimeout), ValidateConfiguration);
            TimeToRestartAuditIngestionAfterFailure = GetTimeToRestartIngestionAfterFailure("TimeToRestartAuditIngestionAfterFailure");
            AuditIngestionBatchSize = IngestionSettingsReader.ReadBatchSize(SettingsRootNamespace, nameof(AuditIngestionBatchSize), ValidateConfiguration);
            AuditIngestionMaxParallelWriters = IngestionSettingsReader.ReadMaxParallelWriters(SettingsRootNamespace, nameof(AuditIngestionMaxParallelWriters), ValidateConfiguration);
            AuditIngestionBatchTimeout = IngestionSettingsReader.ReadBatchTimeout(SettingsRootNamespace, nameof(AuditIngestionBatchTimeout), ValidateConfiguration);
            DisableExternalIntegrationsPublishing = SettingsReader.Read(SettingsRootNamespace, "DisableExternalIntegrationsPublishing", false);
            TrackInstancesInitialValue = SettingsReader.Read(SettingsRootNamespace, "TrackInstancesInitialValue", true);
            ShutdownTimeout = SettingsReader.Read(SettingsRootNamespace, "ShutdownTimeout", ShutdownTimeout);
            AssemblyLoadContextResolver = static assemblyPath => new PluginAssemblyLoadContext(assemblyPath);
        }

        [JsonIgnore]
        public Func<string, AssemblyLoadContext> AssemblyLoadContextResolver { get; set; }

        public LoggingSettings LoggingSettings { get; }

        /// <summary>
        /// OIDC authentication for API access via ServicePulse
        /// </summary>
        public OpenIdConnectSettings OpenIdConnectSettings { get; }

        /// <summary>
        /// X-Forwarded-* header processing for reverse proxy scenarios
        /// </summary>
        public ForwardedHeadersSettings ForwardedHeadersSettings { get; }

        /// <summary>
        /// HTTPS/TLS and HSTS configuration
        /// </summary>
        public HttpsSettings HttpsSettings { get; }

        /// <summary>
        /// Cross-origin resource sharing policy
        /// </summary>
        public CorsSettings CorsSettings { get; }

        public string NotificationsFilter { get; set; }

        public bool AllowMessageEditing { get; set; }

        public bool EnableIntegratedServicePulse { get; set; }
        public ServicePulseSettings ServicePulseSettings { get; set; }

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

                // Use HTTPS scheme if TLS is enabled, otherwise HTTP
                var scheme = HttpsSettings.Enabled ? "https" : "http";
                return $"{scheme}://{Hostname}:{Port}/{suffix}";
            }
        }

        public string ApiUrl => $"{RootUrl}api";

        public string InstanceId
        {
            get
            {
                if (string.IsNullOrEmpty(field))
                {
                    field = InstanceIdGenerator.FromApiUrl(ApiUrl);
                }

                return field;
            }
        }

        public string StorageUrl => $"{RootUrl}storage";

        public string StagingQueue => $"{InstanceName}.staging";

        public int Port { get; private set; }

        public PersistenceSettings PersisterSpecificSettings { get; set; }

        public bool PrintMetrics => SettingsReader.Read<bool>(SettingsRootNamespace, "PrintMetrics");

        public string OtlpEndpointUrl { get; set; } = SettingsReader.Read<string>(SettingsRootNamespace, nameof(OtlpEndpointUrl));
        public string Hostname { get; private set; }
        public string VirtualDirectory => SettingsReader.Read(SettingsRootNamespace, "VirtualDirectory", string.Empty);

        public TimeSpan HeartbeatGracePeriod { get; set; }

        public string TransportType { get; set; }
        public string PersistenceType { get; private set; }
        public string ErrorLogQueue { get; set; }
        public string ErrorQueue { get; set; }

        public bool ForwardErrorMessages { get; set; }

        public bool IngestErrorMessages { get; set; } = true;
        public bool RunRetryProcessor { get; set; } = true;

        public string AuditQueue { get; set; }
        public string AuditLogQueue { get; set; }

        public bool ForwardAuditMessages { get; set; }

        /// <summary>
        /// Whether the normal primary host runs the audit receiver. Only has an effect where the
        /// persister advertises audit support; always on under audit ingestion only.
        /// </summary>
        public bool IngestAuditMessages { get; set; } = true;

        public int? AuditIngestionBatchSize { get; set; }
        public int? AuditIngestionMaxParallelWriters { get; set; }
        public TimeSpan AuditIngestionBatchTimeout { get; set; }

        public TimeSpan TimeToRestartAuditIngestionAfterFailure { get; set; }

        // Set by the --error-ingestion-only command, never read from configuration.
        public bool ErrorIngestionOnly { get; set; }

        // Set by the --audit-ingestion-only command, never read from configuration.
        public bool AuditIngestionOnly { get; set; }

        /// <summary>
        /// True in either ingestion only mode. These hosts run no NServiceBus endpoint, own none of the
        /// work a deployment may only do once, and never provision anything.
        /// </summary>
        [JsonIgnore]
        public bool IngestionOnly => ErrorIngestionOnly || AuditIngestionOnly;

        public TimeSpan? AuditRetentionPeriod { get; set; }

        public TimeSpan ErrorRetentionPeriod { get; }

        public TimeSpan EventsRetentionPeriod { get; }

        public string InstanceName { get; init; } = DEFAULT_INSTANCE_NAME;
        public bool TrackInstancesInitialValue { get; set; }

        public string TransportConnectionString { get; set; }
        public TimeSpan ProcessRetryBatchesFrequency { get; set; }
        public TimeSpan TimeToRestartErrorIngestionAfterFailure { get; set; }

        /// <summary>
        /// The most messages one write handles. Null leaves it to the transport's concurrency.
        /// </summary>
        public int? ErrorIngestionBatchSize { get; set; }

        /// <summary>
        /// How many batches are written at once. Null leaves it to the storage, and a storage whose
        /// batches are not safe to interleave holds it at one whatever is configured.
        /// </summary>
        public int? ErrorIngestionMaxParallelWriters { get; set; }

        /// <summary>
        /// How long a batch that is not yet full waits for more messages.
        /// </summary>
        public TimeSpan ErrorIngestionBatchTimeout { get; set; }
        public int? MaximumConcurrencyLevel { get; set; }

        public int RetryHistoryDepth { get; set; }

        public RemoteInstanceSetting[] RemoteInstances { get; set; }

        public bool DisableHealthChecks { get; set; }

        // The default value is set to the maximum allowed time by the most
        // restrictive hosting platform, which is Linux containers. Linux
        // containers allow for a maximum of 10 seconds. We set it to 5 to
        // allow for cancellation and logging to take place
        public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);

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

        public TransportSettings ToTransportSettings(ComponentInstallationContext installationContext)
        {
            var transportSettings = new TransportSettings
            {
                EndpointName = InstanceName,
                ConnectionString = TransportConnectionString,
                MaxConcurrency = MaximumConcurrencyLevel,
                RunCustomChecks = true,
                TransportType = TransportType,
                AssemblyLoadContextResolver = AssemblyLoadContextResolver,
                EventTypesPublished = installationContext.EventTypesPublished,
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
                        message = "EventRetentionPeriod settings is invalid, value should be minimum 1 hour";
                        logger.LogCritical(message);
                        throw new Exception(message);
                    }

                    if (ValidateConfiguration && result > TimeSpan.FromDays(200))
                    {
                        message = "EventRetentionPeriod settings is invalid, value should be maximum 200 days";
                        logger.LogCritical(message);
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
                message = "ErrorRetentionPeriod settings is missing, please make sure it is included";
                logger.LogCritical(message);
                throw new Exception(message);
            }

            if (TimeSpan.TryParse(valueRead, out var result))
            {
                if (ValidateConfiguration && result < TimeSpan.FromDays(5))
                {
                    message = "ErrorRetentionPeriod settings is invalid, value should be minimum 5 days";
                    logger.LogCritical(message);
                    throw new Exception(message);
                }

                if (ValidateConfiguration && result > TimeSpan.FromDays(45))
                {
                    message = "ErrorRetentionPeriod settings is invalid, value should be maximum 45 days";
                    logger.LogCritical(message);
                    throw new Exception(message);
                }
            }
            else
            {
                message = "ErrorRetentionPeriod settings is invalid, please make sure it is a TimeSpan";
                logger.LogCritical(message);
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

        TimeSpan GetHeartbeatGracePeriod()
        {
            try
            {
                return TimeSpan.Parse(SettingsReader.Read(SettingsRootNamespace, "HeartbeatGracePeriod", "00:00:40"));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "HeartbeatGracePeriod settings invalid. Defaulting HeartbeatGracePeriod to '00:00:40'");
                return TimeSpan.FromSeconds(40);
            }
        }

        TimeSpan GetTimeToRestartErrorIngestionAfterFailure() => GetTimeToRestartIngestionAfterFailure("TimeToRestartErrorIngestionAfterFailure");

        TimeSpan GetTimeToRestartIngestionAfterFailure(string settingName)
        {
            string message;
            var valueRead = SettingsReader.Read<string>(SettingsRootNamespace, settingName);
            if (valueRead == null)
            {
                return TimeSpan.FromSeconds(60);
            }

            if (TimeSpan.TryParse(valueRead, out var result))
            {
                if (ValidateConfiguration && result < TimeSpan.FromSeconds(5))
                {
                    message = $"{settingName} setting is invalid, value should be minimum 5 seconds.";
                    InternalLogger.Fatal(message);
                    throw new Exception(message);
                }

                if (ValidateConfiguration && result > TimeSpan.FromHours(1))
                {
                    message = $"{settingName} setting is invalid, value should be maximum 1 hour.";
                    InternalLogger.Fatal(message);
                    throw new Exception(message);
                }
            }
            else
            {
                message = $"{settingName} setting is invalid, please make sure it is a TimeSpan.";
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

        void LoadAuditIngestionSettings()
        {
            // Key names are deliberately the ones the standalone audit instance reads, so an operator
            // configures a combined primary exactly as they configure an audit instance today.
            var serviceBusRootNamespace = new SettingsRootNamespace("ServiceBus");
            AuditQueue = SettingsReader.Read(serviceBusRootNamespace, "AuditQueue", "audit");

            if (string.IsNullOrEmpty(AuditQueue))
            {
                throw new Exception("ServiceBus/AuditQueue value is required to start the instance");
            }

            IngestAuditMessages = SettingsReader.Read(SettingsRootNamespace, "IngestAuditMessages", true);

            AuditLogQueue = SettingsReader.Read<string>(serviceBusRootNamespace, "AuditLogQueue", null);

            if (AuditLogQueue == null)
            {
                logger.LogInformation("No settings found for audit log queue to import, default name will be used");
                AuditLogQueue = Subscope(AuditQueue);
            }

            ForwardAuditMessages = SettingsReader.Read(SettingsRootNamespace, "ForwardAuditMessages", false);
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
                logger.LogInformation("Error ingestion disabled");
            }

            ErrorLogQueue = SettingsReader.Read<string>(serviceBusRootNamespace, "ErrorLogQueue", null);

            if (ErrorLogQueue == null)
            {
                logger.LogInformation("No settings found for error log queue to import, default name will be used");
                ErrorLogQueue = Subscope(ErrorQueue);
            }
        }

        // logger is intentionally not static to prevent it from being initialized before LoggingConfigurator.ConfigureLogging has been called
        readonly ILogger logger = LoggerUtil.CreateStaticLogger<Settings>();

        public const string DEFAULT_INSTANCE_NAME = "Particular.ServiceControl";
        public static readonly SettingsRootNamespace SettingsRootNamespace = new("ServiceControl");
    }
}