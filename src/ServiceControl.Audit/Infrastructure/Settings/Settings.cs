namespace ServiceControl.Audit.Infrastructure.Settings
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using NLog.Common;
    using NServiceBus.Logging;
    using Transports;

    public class Settings
    {
        public Settings(string serviceName = null)
        {
            ServiceName = serviceName;

            if (string.IsNullOrEmpty(serviceName))
            {
                ServiceName = DEFAULT_SERVICE_NAME;
            }

            {
                // order matters
                AuditQueue = GetAuditQueue();
                AuditLogQueue = GetAuditLogQueue();
            }

            TryLoadLicenseFromConfig();

            TransportConnectionString = GetConnectionString();
            TransportCustomizationType = GetTransportType();
            ForwardAuditMessages = GetForwardAuditMessages();
            AuditRetentionPeriod = GetAuditRetentionPeriod();
            ExpirationProcessTimerInSeconds = GetExpirationProcessTimer();
            Port = SettingsReader<int>.Read("Port", 44444);
            DatabaseMaintenancePort = SettingsReader<int>.Read("DatabaseMaintenancePort", 44445);
            MaximumConcurrencyLevel = SettingsReader<int>.Read("MaximumConcurrencyLevel", 32);
            HttpDefaultConnectionLimit = SettingsReader<int>.Read("HttpDefaultConnectionLimit", 100);
            DisableRavenDBPerformanceCounters = SettingsReader<bool>.Read("DisableRavenDBPerformanceCounters", true);
            DbPath = GetDbPath();
            DataSpaceRemainingThreshold = GetDataSpaceRemainingThreshold();
            ServiceControlQueueAddress = SettingsReader<string>.Read("ServiceControlQueueAddress");
            TimeToRestartAuditIngestionAfterFailure = GetTimeToRestartAuditIngestionAfterFailure();
        }

        public Func<string, Dictionary<string, string>, byte[], Func<Task>, Task> OnMessage { get; set; } = (messageId, headers, body, next) => next();

        public bool RunInMemory { get; set; }

        public bool ValidateConfiguration => SettingsReader<bool>.Read("ValidateConfig", true);

        public bool DisableRavenDBPerformanceCounters { get; set; }

        public bool SkipQueueCreation { get; set; }

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

        public string DatabaseMaintenanceUrl
        {
            get { return $"http://{Hostname}:{DatabaseMaintenancePort}"; }
        }

        public string ApiUrl => $"{RootUrl}api";

        public string StorageUrl => $"{RootUrl}storage";

        public int Port { get; set; }
        public int DatabaseMaintenancePort { get; set; }
        public string Hostname => SettingsReader<string>.Read("Hostname", "localhost");
        public string VirtualDirectory => SettingsReader<string>.Read("VirtualDirectory", string.Empty);

        public string TransportCustomizationType { get; set; }
        public string DbPath { get; set; }

        public string AuditQueue { get; set; }

        public bool ForwardAuditMessages { get; set; }

        public bool IngestAuditMessages { get; set; } = true;

        public string AuditLogQueue { get; set; }

        public int ExpirationProcessTimerInSeconds { get; set; }
        
        public TimeSpan AuditRetentionPeriod { get; set; }

        public string LicenseFileText { get; set; }

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

        public string ServiceName { get; }

        public int HttpDefaultConnectionLimit { get; set; }
        public string TransportConnectionString { get; set; }
        public int MaximumConcurrencyLevel { get; set; }
        public int DataSpaceRemainingThreshold { get; set; }

        public string ServiceControlQueueAddress { get; set; }

        public TimeSpan TimeToRestartAuditIngestionAfterFailure { get; set; }

        public TransportCustomization LoadTransportCustomization()
        {
            try
            {
                var customizationType = Type.GetType(TransportCustomizationType, true);
                return (TransportCustomization)Activator.CreateInstance(customizationType);
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load transport customization type {TransportCustomizationType}.", e);
            }
        }

        int GetExpirationProcessTimer()
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

        TimeSpan GetTimeToRestartAuditIngestionAfterFailure()
        {
            string message;
            var valueRead = SettingsReader<string>.Read("TimeToRestartAuditIngestionAfterFailure");
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

        private string GetAuditLogQueue()
        {
            if (AuditQueue == null)
            {
                return null;
            }

            var value = SettingsReader<string>.Read("ServiceBus", "AuditLogQueue", null);

            if (value == null)
            {
                logger.Info("No settings found for audit log queue to import, default name will be used");
                return Subscope(AuditQueue);
            }

            return value;
        }

        private string GetAuditQueue()
        {
            var value = SettingsReader<string>.Read("ServiceBus", "AuditQueue", "audit");

            if (value == null)
            {
                logger.Warn("No settings found for audit queue to import, if this is not intentional please set add ServiceBus/AuditQueue to your appSettings");
                IngestAuditMessages = false;
                return null;
            }

            if (value.Equals(Disabled, StringComparison.OrdinalIgnoreCase))
            {
                logger.Info("Audit ingestion disabled.");
                IngestAuditMessages = false;
                return null; // needs to be null to not create the queues
            }

            return value;
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

        private static bool GetForwardAuditMessages()
        {
            var forwardAuditMessages = NullableSettingsReader<bool>.Read("ForwardAuditMessages");
            if (forwardAuditMessages.HasValue)
            {
                return forwardAuditMessages.Value;
            }

            throw new Exception("ForwardAuditMessages settings is missing, please make sure it is included.");
        }

        static string GetConnectionString()
        {
            var settingsValue = SettingsReader<string>.Read("ConnectionString");
            if (settingsValue != null)
            {
                return settingsValue;
            }

            var connectionStringSettings = ConfigurationManager.ConnectionStrings["NServiceBus/Transport"];
            return connectionStringSettings?.ConnectionString;
        }

        static string GetTransportType()
        {
            var typeName = SettingsReader<string>.Read("TransportType", "ServiceControl.Transports.Msmq.MsmqTransportCustomization, ServiceControl.Transports.Msmq");
            var typeNameAndAssembly = typeName.Split(',');
            if (typeNameAndAssembly.Length < 2)
            {
                throw new Exception($"Configuration of transport Failed. Could not resolve type '{typeName}' from Setting 'TransportType'. Ensure the assembly is present and that type is a fully qualified assembly name");
            }

            string transportAssemblyPath = null;
            try
            {
                transportAssemblyPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), $"{typeNameAndAssembly[1].Trim()}.dll");
                Assembly.LoadFile(transportAssemblyPath); // load into AppDomain
            }
            catch (Exception e)
            {
                throw new Exception($"Configuration of transport Failed. Ensure the assembly '{transportAssemblyPath}' is present and that type is correctly defined in settings", e);
            }

            var transportType = Type.GetType(typeName, false, true);
            if (transportType != null)
            {
                return typeName;
            }

            throw new Exception($"Configuration of transport Failed. Could not resolve type '{typeName}' from Setting 'TransportType'. Ensure the assembly is present and that type is correctly defined in settings");
        }

        private static string SanitiseFolderName(string folderName)
        {
            return Path.GetInvalidPathChars().Aggregate(folderName, (current, c) => current.Replace(c, '-'));
        }

        TimeSpan GetAuditRetentionPeriod()
        {
            string message;
            var valueRead = SettingsReader<string>.Read("AuditRetentionPeriod");
            if (valueRead == null)
            {
                message = "AuditRetentionPeriod settings is missing, please make sure it is included.";
                logger.Fatal(message);
                throw new Exception(message);
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

        void TryLoadLicenseFromConfig()
        {
            LicenseFileText = SettingsReader<string>.Read("LicenseText");
        }

        ILog logger = LogManager.GetLogger(typeof(Settings));
        int expirationProcessTimerInSeconds = SettingsReader<int>.Read("ExpirationProcessTimerInSeconds", ExpirationProcessTimerInSecondsDefault);
        int maxBodySizeToStore = SettingsReader<int>.Read("MaxBodySizeToStore", MaxBodySizeToStoreDefault);
        public const string DEFAULT_SERVICE_NAME = "Particular.ServiceControl.Audit";
        public const string Disabled = "!disable";

        const int ExpirationProcessTimerInSecondsDefault = 600;
        const int MaxBodySizeToStoreDefault = 102400; //100 kb
        const int DataSpaceRemainingThresholdDefault = 20;
    }
}