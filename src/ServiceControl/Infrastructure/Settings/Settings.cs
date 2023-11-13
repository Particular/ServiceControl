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
    using ServiceControl.Transports;

    public class Settings
    {
        public Settings(string serviceName = null)
        {
            ServiceName = serviceName;

            if (string.IsNullOrEmpty(serviceName))
            {
                ServiceName = DEFAULT_SERVICE_NAME;
            }

            // Overwrite the service name if it is specified in ENVVAR, reg, or config file
            ServiceName = SettingsReader<string>.Read("InternalQueueName", ServiceName);

            LoadErrorIngestionSettings();

            TryLoadLicenseFromConfig();

            TransportConnectionString = GetConnectionString();
            TransportType = SettingsReader<string>.Read("TransportType");
            AuditRetentionPeriod = GetAuditRetentionPeriod();
            ForwardErrorMessages = GetForwardErrorMessages();
            ErrorRetentionPeriod = GetErrorRetentionPeriod();
            EventsRetentionPeriod = GetEventRetentionPeriod();
            Port = SettingsReader<int>.Read("Port", 33333);
            DatabaseMaintenancePort = SettingsReader<int>.Read("DatabaseMaintenancePort", 33334);
            ProcessRetryBatchesFrequency = TimeSpan.FromSeconds(30);
            MaximumConcurrencyLevel = SettingsReader<int>.Read("MaximumConcurrencyLevel", 10);
            RetryHistoryDepth = SettingsReader<int>.Read("RetryHistoryDepth", 10);
            HttpDefaultConnectionLimit = SettingsReader<int>.Read("HttpDefaultConnectionLimit", 100);
            AllowMessageEditing = SettingsReader<bool>.Read("AllowMessageEditing");
            NotificationsFilter = SettingsReader<string>.Read("NotificationsFilter");
            RemoteInstances = GetRemoteInstances().ToArray();
            DataSpaceRemainingThreshold = GetDataSpaceRemainingThreshold();
            MinimumStorageLeftRequiredForIngestion = GetMinimumStorageLeftRequiredForIngestion();
            DbPath = GetDbPath();
            TimeToRestartErrorIngestionAfterFailure = GetTimeToRestartErrorIngestionAfterFailure();
            DisableExternalIntegrationsPublishing = SettingsReader<bool>.Read("DisableExternalIntegrationsPublishing", false);
            EnableFullTextSearchOnBodies = SettingsReader<bool>.Read("EnableFullTextSearchOnBodies", true);
            DataStoreType = GetDataStoreType();
        }

        void LoadErrorIngestionSettings()
        {
            ErrorQueue = SettingsReader<string>.Read("ServiceBus", "ErrorQueue", "error");

            var hasValidErrorQueueName = !string.IsNullOrEmpty(ErrorQueue) && !ErrorQueue.Equals(Disabled, StringComparison.OrdinalIgnoreCase);

            IngestErrorMessages = !SettingsReader<bool>.Read("DisableErrorQueueIngestion", !hasValidErrorQueueName);

            if (IngestErrorMessages && hasValidErrorQueueName == false)
            {
                throw new Exception($"Error ingestion cannot be enabled when ServiceBus/ErrorQueue is '{ErrorQueue}'");
            }

            if (hasValidErrorQueueName == false || IngestErrorMessages == false)
            {
                logger.Info("Error ingestion disabled.");
            }

            if (hasValidErrorQueueName == false)
            {
                if (string.IsNullOrEmpty(ErrorQueue))
                {
                    logger.Warn("No configuration value set for ServiceBus/ErrorQueue. If this is not intentional provide a value.");
                }

                ErrorLogQueue = null;
                ErrorQueue = null;
            }
            else
            {
                var errorLogQueue = SettingsReader<string>.Read("ServiceBus", "ErrorLogQueue", null);

                if (errorLogQueue == null)
                {
                    logger.Info("No settings found for error log queue to import, default name will be used");

                    ErrorLogQueue = Subscope(ErrorQueue);
                }
                else
                {
                    ErrorLogQueue = errorLogQueue;
                }
            }
        }

        public string NotificationsFilter { get; set; }

        public bool AllowMessageEditing { get; set; }

        //HINT: acceptance tests only
        public Func<MessageContext, bool> MessageFilter { get; set; }

        //HINT: acceptance tests only
        public bool RunInMemory { get; set; }

        //HINT: acceptance tests only
        public string EmailDropFolder { get; set; }

        public bool ValidateConfiguration => SettingsReader<bool>.Read("ValidateConfig", true);

        public int ExternalIntegrationsDispatchingBatchSize => SettingsReader<int>.Read("ExternalIntegrationsDispatchingBatchSize", 100);

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

        public string DatabaseMaintenanceUrl => $"http://{Hostname}:{DatabaseMaintenancePort}";

        public string ApiUrl => $"{RootUrl}api";

        public string StorageUrl => $"{RootUrl}storage";

        public string StagingQueue => $"{ServiceName}.staging";

        public int Port { get; set; }
        public int DatabaseMaintenancePort { get; set; }

        public string LicenseFileText { get; set; }

        public bool ExposeRavenDB => SettingsReader<bool>.Read("ExposeRavenDB");
        public bool PrintMetrics => SettingsReader<bool>.Read("PrintMetrics");
        public string Hostname => SettingsReader<string>.Read("Hostname", "localhost");
        public string VirtualDirectory => SettingsReader<string>.Read("VirtualDirectory", string.Empty);

        public TimeSpan HeartbeatGracePeriod
        {
            get
            {
                try
                {
                    return TimeSpan.Parse(SettingsReader<string>.Read("HeartbeatGracePeriod", "00:00:40"));
                }
                catch (Exception ex)
                {
                    logger.Error($"HeartbeatGracePeriod settings invalid - {ex}. Defaulting HeartbeatGracePeriod to '00:00:40'");
                    return TimeSpan.FromSeconds(40);
                }
            }
        }

        public string TransportType { get; set; }
        public string DbPath { get; set; }
        public string ErrorLogQueue { get; set; }
        public string ErrorQueue { get; set; }

        public bool ForwardErrorMessages { get; set; }

        public bool IngestErrorMessages { get; set; } = true;
        public bool RunRetryProcessor { get; set; } = true;

        public int ExpirationProcessTimerInSeconds
        {
            get
            {
                if (expirationProcessTimerInSeconds < 0)
                {
                    logger.Error($"ExpirationProcessTimerInSeconds cannot be negative. Defaulting to {ExpirationProcessTimerInSecondsDefault}");
                    return ExpirationProcessTimerInSecondsDefault;
                }

                if (ValidateConfiguration && expirationProcessTimerInSeconds > TimeSpan.FromHours(3).TotalSeconds)
                {
                    logger.Error($"ExpirationProcessTimerInSeconds cannot be larger than {TimeSpan.FromHours(3).TotalSeconds}. Defaulting to {ExpirationProcessTimerInSecondsDefault}");
                    return ExpirationProcessTimerInSecondsDefault;
                }

                return expirationProcessTimerInSeconds;
            }
        }

        public TimeSpan? AuditRetentionPeriod { get; }

        public TimeSpan ErrorRetentionPeriod { get; }

        public TimeSpan EventsRetentionPeriod { get; }

        public int ExpirationProcessBatchSize
        {
            get
            {
                if (expirationProcessBatchSize < 1)
                {
                    logger.Error($"ExpirationProcessBatchSize cannot be less than 1. Defaulting to {ExpirationProcessBatchSizeDefault}");
                    return ExpirationProcessBatchSizeDefault;
                }

                if (ValidateConfiguration && expirationProcessBatchSize < ExpirationProcessBatchSizeMinimum)
                {
                    logger.Error($"ExpirationProcessBatchSize cannot be less than {ExpirationProcessBatchSizeMinimum}. Defaulting to {ExpirationProcessBatchSizeDefault}");
                    return ExpirationProcessBatchSizeDefault;
                }

                return expirationProcessBatchSize;
            }
        }

        public string ServiceName { get; }

        public int HttpDefaultConnectionLimit { get; set; }
        public string TransportConnectionString { get; set; }
        public TimeSpan ProcessRetryBatchesFrequency { get; set; }
        public TimeSpan TimeToRestartErrorIngestionAfterFailure { get; set; }
        public int MaximumConcurrencyLevel { get; set; }

        public int RetryHistoryDepth { get; set; }

        public RemoteInstanceSetting[] RemoteInstances { get; set; }

        public int DataSpaceRemainingThreshold { get; set; }
        public int MinimumStorageLeftRequiredForIngestion { get; set; }

        public bool EnableFullTextSearchOnBodies { get; set; }

        public bool DisableHealthChecks { get; set; }

        public bool ExposeApi { get; set; } = true;

        public DataStoreType DataStoreType { get; set; } = DataStoreType.RavenDB35;

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
            var settingsValue = SettingsReader<string>.Read("ConnectionString");
            if (settingsValue != null)
            {
                return settingsValue;
            }

            var connectionStringSettings = ConfigurationManager.ConnectionStrings["NServiceBus/Transport"];
            return connectionStringSettings?.ConnectionString;
        }

        string GetDbPath()
        {
            var host = Hostname;
            if (host == "*")
            {
                host = "%";
            }

            var dbFolder = $"{host}-{Port}";

            if (!string.IsNullOrEmpty(VirtualDirectory))
            {
                dbFolder += $"-{SanitiseFolderName(VirtualDirectory)}";
            }

            var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Particular", "ServiceControl", dbFolder);

            return SettingsReader<string>.Read("DbPath", defaultPath);
        }

        static bool GetForwardErrorMessages()
        {
            var forwardErrorMessages = NullableSettingsReader<bool>.Read("ForwardErrorMessages");
            if (forwardErrorMessages.HasValue)
            {
                return forwardErrorMessages.Value;
            }

            throw new Exception("ForwardErrorMessages settings is missing, please make sure it is included.");
        }

        static string SanitiseFolderName(string folderName)
        {
            return Path.GetInvalidPathChars().Aggregate(folderName, (current, c) => current.Replace(c, '-'));
        }

        TimeSpan GetEventRetentionPeriod()
        {
            var valueRead = SettingsReader<string>.Read("EventRetentionPeriod");
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
            var valueRead = SettingsReader<string>.Read("ErrorRetentionPeriod");
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
            var valueRead = SettingsReader<string>.Read("AuditRetentionPeriod");
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
            var valueRead = SettingsReader<string>.Read("TimeToRestartErrorIngestionAfterFailure");
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
            var valueRead = SettingsReader<string>.Read("RemoteInstances");
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
            var threshold = SettingsReader<int>.Read("DataSpaceRemainingThreshold", DataSpaceRemainingThresholdDefault);
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

        int GetMinimumStorageLeftRequiredForIngestion()
        {
            string message;
            var threshold = SettingsReader<int>.Read("MinimumStorageLeftRequiredForIngestion", MinimumStorageLeftRequiredForIngestionDefault);
            if (threshold < 0)
            {
                message = $"{nameof(MinimumStorageLeftRequiredForIngestion)} is invalid, minimum value is 0.";
                logger.Fatal(message);
                throw new Exception(message);
            }

            if (threshold > 100)
            {
                message = $"{nameof(MinimumStorageLeftRequiredForIngestion)} is invalid, maximum value is 100.";
                logger.Fatal(message);
                throw new Exception(message);
            }

            return threshold;
        }

        DataStoreType GetDataStoreType()
        {
            var value = SettingsReader<string>.Read("DataStoreType", "RavenDB35");

            return (DataStoreType)Enum.Parse(typeof(DataStoreType), value);
        }

        void TryLoadLicenseFromConfig()
        {
            LicenseFileText = SettingsReader<string>.Read("LicenseText");
        }

        ILog logger = LogManager.GetLogger(typeof(Settings));
        int expirationProcessBatchSize = SettingsReader<int>.Read("ExpirationProcessBatchSize", ExpirationProcessBatchSizeDefault);
        int expirationProcessTimerInSeconds = SettingsReader<int>.Read("ExpirationProcessTimerInSeconds", ExpirationProcessTimerInSecondsDefault);
        public const string DEFAULT_SERVICE_NAME = "Particular.ServiceControl";
        public const string Disabled = "!disable";

        const int ExpirationProcessTimerInSecondsDefault = 600;
        const int ExpirationProcessBatchSizeDefault = 65512;
        const int ExpirationProcessBatchSizeMinimum = 10240;
        const int DataSpaceRemainingThresholdDefault = 20;
        const int MinimumStorageLeftRequiredForIngestionDefault = 5;
    }
}