namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using Newtonsoft.Json;
    using NLog.Common;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using ServiceControl.Infrastructure.WebApi;
    using ServiceControl.Persistence;
    using ServiceControl.Transports;

    public class Settings
    {
        public Settings(
            string serviceName = null,
            string transportType = null,
            string persisterType = null,
            bool? forwardErrorMessages = default,
            TimeSpan? errorRetentionPeriod = default
            )
        {
            ServiceName = serviceName;

            if (string.IsNullOrEmpty(serviceName))
            {
                ServiceName = DEFAULT_SERVICE_NAME;
            }

            // Overwrite the service name if it is specified in ENVVAR, reg, or config file
            ServiceName = SettingsReader.Read("InternalQueueName", ServiceName);

            ErrorQueue = GetErrorQueue();
            ErrorLogQueue = GetErrorLogQueue(ErrorQueue);

            TryLoadLicenseFromConfig();

            TransportConnectionString = GetConnectionString();
            TransportType = transportType ?? SettingsReader.Read<string>("TransportType");
            PersistenceType = persisterType ?? SettingsReader.Read<string>("PersistenceType");
            AuditRetentionPeriod = GetAuditRetentionPeriod();
            ForwardErrorMessages = forwardErrorMessages ?? GetForwardErrorMessages();
            ErrorRetentionPeriod = errorRetentionPeriod ?? GetErrorRetentionPeriod();
            EventsRetentionPeriod = GetEventRetentionPeriod();
            Port = SettingsReader.Read("Port", 33333);
            ProcessRetryBatchesFrequency = TimeSpan.FromSeconds(30);
            MaximumConcurrencyLevel = SettingsReader.Read("MaximumConcurrencyLevel", 10);
            RetryHistoryDepth = SettingsReader.Read("RetryHistoryDepth", 10);
            HttpDefaultConnectionLimit = SettingsReader.Read("HttpDefaultConnectionLimit", 100);
            AllowMessageEditing = SettingsReader.Read<bool>("AllowMessageEditing");
            NotificationsFilter = SettingsReader.Read<string>("NotificationsFilter");
            RemoteInstances = GetRemoteInstances().ToArray();
            DataSpaceRemainingThreshold = GetDataSpaceRemainingThreshold();
            TimeToRestartErrorIngestionAfterFailure = GetTimeToRestartErrorIngestionAfterFailure();
            DisableExternalIntegrationsPublishing = SettingsReader.Read("DisableExternalIntegrationsPublishing", false);
        }

        public string NotificationsFilter { get; set; }

        public bool AllowMessageEditing { get; set; }

        //HINT: acceptance tests only
        public Func<MessageContext, bool> MessageFilter { get; set; }

        //HINT: acceptance tests only
        public string EmailDropFolder { get; set; }

        public bool ValidateConfiguration => SettingsReader.Read("ValidateConfig", true);

        public int ExternalIntegrationsDispatchingBatchSize => SettingsReader.Read("ExternalIntegrationsDispatchingBatchSize", 100);

        public bool DisableExternalIntegrationsPublishing { get; set; }

        public bool SkipQueueCreation { get; set; }

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

        public string StorageUrl => $"{RootUrl}storage";

        public string StagingQueue => $"{ServiceName}.staging";

        public int Port { get; set; }

        public string LicenseFileText { get; set; }

        public PersistenceSettings PersisterSpecificSettings { get; set; }

        public bool PrintMetrics => SettingsReader.Read<bool>("PrintMetrics");
        public string Hostname => SettingsReader.Read("Hostname", "localhost");
        public string VirtualDirectory => SettingsReader.Read("VirtualDirectory", string.Empty);

        public TimeSpan HeartbeatGracePeriod
        {
            get
            {
                try
                {
                    return TimeSpan.Parse(SettingsReader.Read("HeartbeatGracePeriod", "00:00:40"));
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
        public string ServiceName { get; }

        public int HttpDefaultConnectionLimit { get; set; }
        public string TransportConnectionString { get; set; }
        public TimeSpan ProcessRetryBatchesFrequency { get; set; }
        public TimeSpan TimeToRestartErrorIngestionAfterFailure { get; set; }
        public int MaximumConcurrencyLevel { get; set; }

        public int RetryHistoryDepth { get; set; }

        public RemoteInstanceSetting[] RemoteInstances { get; set; }

        public int DataSpaceRemainingThreshold { get; set; }

        public bool DisableHealthChecks { get; set; }

        public bool ExposeApi { get; set; } = true;

        public TransportCustomization LoadTransportCustomization()
        {
            try
            {
                TransportType = TransportManifestLibrary.Find(TransportType);

                var customizationType = Type.GetType(TransportType, true);
                return (TransportCustomization)Activator.CreateInstance(customizationType);
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load transport customization type {TransportType}.", e);
            }
        }

        public string GetConnectionString()
        {
            var settingsValue = SettingsReader.Read<string>("ConnectionString");
            if (settingsValue != null)
            {
                return settingsValue;
            }

            var connectionStringSettings = ConfigurationManager.ConnectionStrings["NServiceBus/Transport"];
            return connectionStringSettings?.ConnectionString;
        }

        string GetErrorQueue()
        {
            var value = SettingsReader.Read("ServiceBus", "ErrorQueue", "error");

            if (value == null)
            {
                logger.Warn("No settings found for error queue to import, if this is not intentional please set add ServiceBus/ErrorQueue to your appSettings");
                IngestErrorMessages = false;
                return null;
            }

            if (value.Equals(Disabled, StringComparison.OrdinalIgnoreCase))
            {
                logger.Info("Error ingestion disabled.");
                IngestErrorMessages = false;
                return null; // needs to be null to not create the queues
            }

            return value;
        }

        string GetErrorLogQueue(string errorQueue)
        {
            if (errorQueue == null)
            {
                return null;
            }

            var value = SettingsReader.Read<string>("ServiceBus", "ErrorLogQueue", null);

            if (value == null)
            {
                logger.Info("No settings found for error log queue to import, default name will be used");
                return Subscope(errorQueue);
            }

            return value;
        }

        static bool GetForwardErrorMessages()
        {
            var forwardErrorMessages = SettingsReader.Read<bool?>("ForwardErrorMessages");
            if (forwardErrorMessages.HasValue)
            {
                return forwardErrorMessages.Value;
            }

            throw new Exception("ForwardErrorMessages settings is missing, please make sure it is included.");
        }

        TimeSpan GetEventRetentionPeriod()
        {
            var valueRead = SettingsReader.Read<string>("EventRetentionPeriod");
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
            var valueRead = SettingsReader.Read<string>("ErrorRetentionPeriod");
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
            var valueRead = SettingsReader.Read<string>("AuditRetentionPeriod");
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
            var valueRead = SettingsReader.Read<string>("TimeToRestartErrorIngestionAfterFailure");
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
            var valueRead = SettingsReader.Read<string>("RemoteInstances");
            if (!string.IsNullOrEmpty(valueRead))
            {
                return ParseRemoteInstances(valueRead);
            }

            return Array.Empty<RemoteInstanceSetting>();
        }

        internal static IList<RemoteInstanceSetting> ParseRemoteInstances(string value)
        {
            var jsonSerializer = JsonSerializer.Create(JsonNetSerializerSettings.CreateDefault());
            using (var jsonReader = new JsonTextReader(new StringReader(value)))
            {
                return jsonSerializer.Deserialize<RemoteInstanceSetting[]>(jsonReader) ?? new RemoteInstanceSetting[0];
            }
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

        int GetDataSpaceRemainingThreshold()
        {
            string message;
            var threshold = SettingsReader.Read("DataSpaceRemainingThreshold", DataSpaceRemainingThresholdDefault);
            if (threshold < 0)
            {
                message = $"{nameof(DataSpaceRemainingThreshold)} is invalid, minimum value is 0.";
                logger.Fatal(message);
                throw new Exception(message);
            }

            if (threshold > 100)
            {
                message = $"{nameof(DataSpaceRemainingThreshold)} is invalid, maximum value is 100.";
                logger.Fatal(message);
                throw new Exception(message);
            }

            return threshold;
        }

        void TryLoadLicenseFromConfig()
        {
            LicenseFileText = SettingsReader.Read<string>("LicenseText");
        }

        static readonly ILog logger = LogManager.GetLogger(typeof(Settings));
        public const string DEFAULT_SERVICE_NAME = "Particular.ServiceControl";
        public const string Disabled = "!disable";

        const int DataSpaceRemainingThresholdDefault = 20;
    }
}