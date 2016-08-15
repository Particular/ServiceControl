namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;
    using System.IO;
    using System.Linq;
    using NLog.Common;
    using NServiceBus;
    using NServiceBus.Logging;

    public class Settings
    {
        public const string DEFAULT_SERVICE_NAME = "Particular.ServiceControl";

        private const int ExpirationProcessTimerInSecondsDefault = 600;
        private const int ExpirationProcessBatchSizeDefault = 65512;
        private const int ExpirationProcessBatchSizeMinimum = 10240;
        private const int MaxBodySizeToStoreDefault = 102400; //100 kb

        private ILog logger = LogManager.GetLogger(typeof(Settings));
        private int expirationProcessBatchSize = SettingsReader<int>.Read("ExpirationProcessBatchSize", ExpirationProcessBatchSizeDefault);
        private int expirationProcessTimerInSeconds = SettingsReader<int>.Read("ExpirationProcessTimerInSeconds", ExpirationProcessTimerInSecondsDefault);
        private int maxBodySizeToStore = SettingsReader<int>.Read("MaxBodySizeToStore", MaxBodySizeToStoreDefault);

        public Settings(string serviceName = null)
        {
            ServiceName = serviceName;

            if (string.IsNullOrEmpty(serviceName))
            {
                ServiceName = DEFAULT_SERVICE_NAME;
            }

            AuditQueue = GetAuditQueue();
            ErrorQueue = GetErrorQueue();
            ErrorLogQueue = GetErrorLogQueue();
            AuditLogQueue = GetAuditLogQueue();
            DbPath = GetDbPath();
            TransportType = SettingsReader<string>.Read("TransportType", typeof(MsmqTransport).AssemblyQualifiedName);
            ForwardAuditMessages = GetForwardAuditMessages();
            ForwardErrorMessages = GetForwardErrorMessages();
            AuditRetentionPeriod = GetAuditRetentionPeriod();
            ErrorRetentionPeriod = GetErrorRetentionPeriod();
            MaintenanceMode = SettingsReader<bool>.Read("MaintenanceMode");
            Port = SettingsReader<int>.Read("Port", 33333);
            ProcessRetryBatchesFrequency = TimeSpan.FromSeconds(30);
            MaximumConcurrencyLevel = 10;
            HttpDefaultConnectionLimit = SettingsReader<int>.Read("HttpDefaultConnectionLimit", 100);
            DisableRavenDBPerformanceCounters = SettingsReader<bool>.Read("DisableRavenDBPerformanceCounters", true);
        }

        public int ExternalIntegrationsDispatchingBatchSize => SettingsReader<int>.Read("ExternalIntegrationsDispatchingBatchSize", 100);

        public int MaximumMessageThroughputPerSecond => SettingsReader<int>.Read("MaximumMessageThroughputPerSecond", 350);

        public bool MaintenanceMode { get; set; }

        public bool DisableRavenDBPerformanceCounters { get; set; }

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

        public int Port { get; set; }

        public bool SetupOnly { get; set; }

        public bool ExposeRavenDB => SettingsReader<bool>.Read("ExposeRavenDB");
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
        public Address ErrorLogQueue { get; set; }
        public Address ErrorQueue { get; }
        public Address AuditQueue { get; }

        public bool ForwardAuditMessages { get; set; }
        public bool ForwardErrorMessages { get; set; }

        public Address AuditLogQueue { get; set; }

        public int ExpirationProcessTimerInSeconds
        {
            get
            {
                if ((expirationProcessTimerInSeconds < 0) || (expirationProcessTimerInSeconds > TimeSpan.FromHours(3).TotalSeconds))
                {
                    logger.Error($"ExpirationProcessTimerInSeconds settings is invalid, the valid range is 0 to {TimeSpan.FromHours(3).TotalSeconds}. Defaulting to {ExpirationProcessTimerInSecondsDefault}");
                    return ExpirationProcessTimerInSecondsDefault;
                }
                return expirationProcessTimerInSeconds;
            }
        }

        public TimeSpan AuditRetentionPeriod { get; }

        public TimeSpan ErrorRetentionPeriod { get; }

        public int ExpirationProcessBatchSize
        {
            get
            {
                if (expirationProcessBatchSize < ExpirationProcessBatchSizeMinimum)
                {
                    logger.Error($"ExpirationProcessBatchSize settings is invalid, {ExpirationProcessBatchSizeMinimum} is the minimum value. Defaulting to {ExpirationProcessBatchSizeDefault}");
                    return ExpirationProcessBatchSizeDefault;
                }
                return expirationProcessBatchSize;
            }
        }

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
            set { maxBodySizeToStore = value; }
        }

        public string ServiceName { get; }

        public int HttpDefaultConnectionLimit { get; set; }
        public string TransportConnectionString { get; set; }
        public TimeSpan ProcessRetryBatchesFrequency { get; set; }
        public int MaximumConcurrencyLevel { get; set; }

        private Address GetAuditLogQueue()
        {
            var value = SettingsReader<string>.Read("ServiceBus", "AuditLogQueue", null);
            if (value == null)
            {
                logger.Info("No settings found for audit log queue to import, default name will be used");
                return AuditQueue.SubScope("log");
            }
            return Address.Parse(value);
        }

        private Address GetAuditQueue()
        {
            var value = SettingsReader<string>.Read("ServiceBus", "AuditQueue", "audit");

            if (value == null)
            {
                logger.Warn("No settings found for audit queue to import, if this is not intentional please set add ServiceBus/AuditQueue to your appSettings");
                return Address.Undefined;
            }
            return Address.Parse(value);
        }

        private Address GetErrorQueue()
        {
            var value = SettingsReader<string>.Read("ServiceBus", "ErrorQueue", "error");

            if (value == null)
            {
                logger.Warn("No settings found for error queue to import, if this is not intentional please set add ServiceBus/ErrorQueue to your appSettings");
                return Address.Undefined;
            }
            return Address.Parse(value);
        }

        private Address GetErrorLogQueue()
        {
            var value = SettingsReader<string>.Read("ServiceBus", "ErrorLogQueue", null);

            if (value == null)
            {
                logger.Info("No settings found for error log queue to import, default name will be used");
                return ErrorQueue.SubScope("log");
            }
            return Address.Parse(value);
        }

        private string GetDbPath()
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

        private static bool GetForwardErrorMessages()
        {
            var forwardErrorMessages = NullableSettingsReader<bool>.Read("ForwardErrorMessages");
            if (forwardErrorMessages.HasValue)
            {
                return forwardErrorMessages.Value;
            }
            throw new Exception("ForwardErrorMessages settings is missing, please make sure it is included.");
        }

        private static bool GetForwardAuditMessages()
        {
            var forwardAuditMessages = NullableSettingsReader<bool>.Read("ForwardAuditMessages");
            if (forwardAuditMessages.HasValue)
            {
                return forwardAuditMessages.Value;
            }
            throw new Exception("ForwardAuditMessages settings is missing, please make sure it is included.");
        }

        private static string SanitiseFolderName(string folderName)
        {
            return Path.GetInvalidPathChars().Aggregate(folderName, (current, c) => current.Replace(c, '-'));
        }

        private TimeSpan GetErrorRetentionPeriod()
        {
            TimeSpan result;
            string message;
            var valueRead = SettingsReader<string>.Read("ErrorRetentionPeriod");
            if (valueRead == null)
            {
                message = "ErrorRetentionPeriod settings is missing, please make sure it is included.";
                logger.Fatal(message);
                throw new Exception(message);
            }

            if (TimeSpan.TryParse(valueRead, out result))
            {
                if (result < TimeSpan.FromDays(10))
                {
                    message = "ErrorRetentionPeriod settings is invalid, value should be minimum 10 days.";
                    logger.Fatal(message);
                    throw new Exception(message);
                }

                if (result > TimeSpan.FromDays(45))
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

        private TimeSpan GetAuditRetentionPeriod()
        {
            TimeSpan result;
            string message;
            var valueRead = SettingsReader<string>.Read("AuditRetentionPeriod");
            if (valueRead == null)
            {
                message = "AuditRetentionPeriod settings is missing, please make sure it is included.";
                logger.Fatal(message);
                throw new Exception(message);
            }

            if (TimeSpan.TryParse(valueRead, out result))
            {
                if (result < TimeSpan.FromHours(1))
                {
                    message = "AuditRetentionPeriod settings is invalid, value should be minimum 1 hour.";
                    InternalLogger.Fatal(message);
                    throw new Exception(message);
                }

                if (result > TimeSpan.FromDays(365))
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
    }
}