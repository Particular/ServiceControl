namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;
    using System.IO;
    using System.Linq;
    using NLog.Common;
    using NServiceBus;
    using NServiceBus.Logging;

    public static class Settings
    {
        static Settings()
        {
            AuditQueue = GetAuditQueue();
            ErrorQueue = GetErrorQueue();
            ErrorLogQueue = GetErrorLogQueue();
            AuditLogQueue = GetAuditLogQueue();
            DbPath = GetDbPath();
            TransportType = SettingsReader<string>.Read("TransportType", typeof(MsmqTransport).AssemblyQualifiedName);
            ForwardAuditMessages = GetForwardAuditMessages();
            ForwardErrorMessages = GetForwardErrorMessages();
            auditRetentionPeriod = GetAuditRetentionPeriod();
            errorRetentionPeriod = GetErrorRetentionPeriod();
        }

        public static string ApiUrl
        {
            get
            {
                var suffix = String.Empty;

                if (!string.IsNullOrEmpty(VirtualDirectory))
                {
                    suffix = VirtualDirectory + "/";
                }

                return $"http://{Hostname}:{Port}/{suffix}";
            }
        }

        public static string StorageUrl
        {
            get
            {
                var suffix = String.Empty;

                if (!string.IsNullOrEmpty(VirtualDirectory))
                {
                    suffix = VirtualDirectory;
                }

                return $"http://{Hostname}:{Port}/{suffix}";
            }
        }

        static Address GetAuditLogQueue()
        {
            var value = SettingsReader<string>.Read("ServiceBus", "AuditLogQueue", null);
            if (value == null)
            {
                Logger.Info("No settings found for audit log queue to import, default name will be used");
                return AuditQueue.SubScope("log");
            }
            return Address.Parse(value);
        }

        static Address GetAuditQueue()
        {
            var value = SettingsReader<string>.Read("ServiceBus", "AuditQueue", "audit");

            if (value == null)
            {
                Logger.Warn(
                    "No settings found for audit queue to import, if this is not intentional please set add ServiceBus/AuditQueue to your appSettings");
                return Address.Undefined;
            }
            return Address.Parse(value);
        }

        static Address GetErrorQueue()
        {
            var value = SettingsReader<string>.Read("ServiceBus", "ErrorQueue", "error");

            if (value == null)
            {
                Logger.Warn(
                    "No settings found for error queue to import, if this is not intentional please set add ServiceBus/ErrorQueue to your appSettings");
                return Address.Undefined;
            }
            return Address.Parse(value);
        }

        static Address GetErrorLogQueue()
        {
            var value = SettingsReader<string>.Read("ServiceBus", "ErrorLogQueue", null);

            if (value == null)
            {
                Logger.Info("No settings found for error log queue to import, default name will be used");
                return ErrorQueue.SubScope("log");
            }
            return Address.Parse(value);
        }

        static string GetDbPath()
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

        static bool GetForwardAuditMessages()
        {
            var forwardAuditMessages = NullableSettingsReader<bool>.Read("ForwardAuditMessages");
            if (forwardAuditMessages.HasValue)
            {
                return forwardAuditMessages.Value;
            }
            throw new Exception("ForwardAuditMessages settings is missing, please make sure it is included.");
        }

        static string SanitiseFolderName(string folderName)
        {
            return Path.GetInvalidPathChars().Aggregate(folderName, (current, c) => current.Replace(c, '-'));
        }

        public static int Port = SettingsReader<int>.Read("Port", 33333);
      
        public static bool ExposeRavenDB = SettingsReader<bool>.Read("ExposeRavenDB");
        public static string Hostname = SettingsReader<string>.Read("Hostname", "localhost");
        public static string VirtualDirectory = SettingsReader<string>.Read("VirtualDirectory", String.Empty);

        public static TimeSpan HeartbeatGracePeriod
        {
            get
            {
                try
                {
                    return TimeSpan.Parse(SettingsReader<string>.Read("HeartbeatGracePeriod", "00:00:40"));
                }
                catch(Exception ex)
                {
                    Logger.ErrorFormat("HeartbeatGracePeriod settings invalid - {0}. Defaulting HeartbeatGracePeriod to '00:00:40'", ex);
                    return TimeSpan.FromSeconds(40);
                }
            }
        }
        
        public static string TransportType { get; set; }

        public static int ExternalIntegrationsDispatchingBatchSize = SettingsReader<int>.Read("ExternalIntegrationsDispatchingBatchSize", 100);

        public static int MaximumMessageThroughputPerSecond = SettingsReader<int>.Read("MaximumMessageThroughputPerSecond", 350);

        public static string DbPath;
        public static Address ErrorLogQueue;
        public static Address ErrorQueue;
        public static Address AuditQueue;

        public static bool ForwardAuditMessages { get; set; }
        public static bool ForwardErrorMessages { get; set; }
        
        public static Address AuditLogQueue;

        const int ExpirationProcessTimerInSecondsDefault = 600;
        static int expirationProcessTimerInSeconds = SettingsReader<int>.Read("ExpirationProcessTimerInSeconds", ExpirationProcessTimerInSecondsDefault);
        public static int ExpirationProcessTimerInSeconds
        {
            get
            {
                if ((expirationProcessTimerInSeconds < 0) || (expirationProcessTimerInSeconds > TimeSpan.FromHours(3).TotalSeconds))
                {
                    Logger.ErrorFormat("ExpirationProcessTimerInSeconds settings is invalid, the valid range is 0 to {0}. Defaulting to {1}", TimeSpan.FromHours(3).TotalSeconds, ExpirationProcessTimerInSecondsDefault);
                    return ExpirationProcessTimerInSecondsDefault;
                }
                return expirationProcessTimerInSeconds;
            }
        }

        static TimeSpan auditRetentionPeriod;
        static TimeSpan errorRetentionPeriod;

        private static TimeSpan GetErrorRetentionPeriod()
        {
            TimeSpan result;
            string message;
            var valueRead = SettingsReader<string>.Read("ErrorRetentionPeriod");
            if (valueRead == null)
            {
                message = "ErrorRetentionPeriod settings is missing, please make sure it is included.";
                Logger.Fatal(message);
                throw new Exception(message);
            }

            if (TimeSpan.TryParse(valueRead, out result))
            { 
                if (result < TimeSpan.FromDays(10))
                {
                    message = "ErrorRetentionPeriod settings is invalid, value should be minimum 10 days.";
                    Logger.Fatal(message);
                    throw new Exception(message);
                }

                if (result > TimeSpan.FromDays(45))
                {
                    message = "ErrorRetentionPeriod settings is invalid, value should be maximum 45 days.";
                    Logger.Fatal(message);
                    throw new Exception(message);
                }
            } 
            else
            { 
                message = "ErrorRetentionPeriod settings is invalid, please make sure it is a TimeSpan.";
                Logger.Fatal(message);
                throw new Exception(message);
            }
            return result;
        }

        private static TimeSpan GetAuditRetentionPeriod()
        {
            TimeSpan result;
            string message;
            var valueRead = SettingsReader<string>.Read("AuditRetentionPeriod");
            if (valueRead == null)
            {
                message = "AuditRetentionPeriod settings is missing, please make sure it is included.";
                Logger.Fatal(message);
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

        public static TimeSpan AuditRetentionPeriod => auditRetentionPeriod;

        public static TimeSpan ErrorRetentionPeriod => errorRetentionPeriod;

        const int ExpirationProcessBatchSizeDefault = 65512;
        const int ExpirationProcessBatchSizeMinimum = 10240;
        static int expirationProcessBatchSize = SettingsReader<int>.Read("ExpirationProcessBatchSize", ExpirationProcessBatchSizeDefault);

        public static int ExpirationProcessBatchSize
        {
            get
            {
                if (expirationProcessBatchSize < ExpirationProcessBatchSizeMinimum)
                {
                    Logger.ErrorFormat("ExpirationProcessBatchSize settings is invalid, {0} is the minimum value. Defaulting to {1}", ExpirationProcessBatchSizeMinimum, ExpirationProcessBatchSizeDefault);
                    return ExpirationProcessBatchSizeDefault;
                }
                return expirationProcessBatchSize;
            }
        }

        const int MaxBodySizeToStoreDefault = 102400; //100 kb

        static int maxBodySizeToStore = SettingsReader<int>.Read("MaxBodySizeToStore", MaxBodySizeToStoreDefault);

        public static int MaxBodySizeToStore
        {
            get
            {
                if (maxBodySizeToStore <= 0)
                {
                    Logger.ErrorFormat("MaxBodySizeToStore settings is invalid, {0} is the minimum value. Defaulting to {1}", 1, MaxBodySizeToStoreDefault);
                    return MaxBodySizeToStoreDefault;
                }
                return maxBodySizeToStore;
            }
            set { maxBodySizeToStore = value; }
        }

        public static ILog Logger = LogManager.GetLogger(typeof(Settings));

        public static string ServiceName;

        public static int HttpDefaultConnectionLimit = SettingsReader<int>.Read("HttpDefaultConnectionLimit", 100);
    }
}
