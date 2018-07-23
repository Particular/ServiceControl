namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Nancy;
    using Newtonsoft.Json;
    using NLog.Common;
    using NServiceBus.Logging;
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

            {
                // order matters
                ErrorQueue = GetErrorQueue();
                ErrorLogQueue = GetErrorLogQueue();

                AuditQueue = GetAuditQueue();
                AuditLogQueue = GetAuditLogQueue();
            }

            var connectionStringSettings = ConfigurationManager.ConnectionStrings["NServiceBus/Transport"];
            TransportConnectionString = connectionStringSettings?.ConnectionString;

            DbPath = GetDbPath();
            TransportCustomizationType = GetTransportType();
            ForwardAuditMessages = GetForwardAuditMessages();
            ForwardErrorMessages = GetForwardErrorMessages();
            AuditRetentionPeriod = GetAuditRetentionPeriod();
            ErrorRetentionPeriod = GetErrorRetentionPeriod();
            EventsRetentionPeriod = GetEventRetentionPeriod();
            Port = SettingsReader<int>.Read("Port", 33333);
            DatabaseMaintenancePort = SettingsReader<int>.Read("DatabaseMaintenancePort", 33334);
            ProcessRetryBatchesFrequency = TimeSpan.FromSeconds(30);
            MaximumConcurrencyLevel = SettingsReader<int>.Read("MaximumConcurrencyLevel", 10);
            RetryHistoryDepth = SettingsReader<int>.Read("RetryHistoryDepth", 10);
            HttpDefaultConnectionLimit = SettingsReader<int>.Read("HttpDefaultConnectionLimit", 100);
            DisableRavenDBPerformanceCounters = SettingsReader<bool>.Read("DisableRavenDBPerformanceCounters", true);
            RemoteInstances = GetRemoteInstances();
        }

        public Func<string, Dictionary<string, string>, byte[], Func<Task>, Task> OnMessage { get; set; } = (messageId, headers, body, next) => next();

        public bool RunInMemory { get; set; }

        public bool ValidateConfiguration => SettingsReader<bool>.Read("ValidateConfig", true);

        public int ExternalIntegrationsDispatchingBatchSize => SettingsReader<int>.Read("ExternalIntegrationsDispatchingBatchSize", 100);

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

        public string TransportCustomizationType { get; set; }

        public string DbPath { get; set; }
        public string ErrorLogQueue { get; set; }
        public string ErrorQueue { get; set; }
        public string AuditQueue { get; set; }

        public bool ForwardAuditMessages { get; set; }
        public bool ForwardErrorMessages { get; set; }

        public bool IngestAuditMessages { get; set; } = true;
        public bool IngestErrorMessages { get; set; } = true;

        public string AuditLogQueue { get; set; }

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

        public TimeSpan AuditRetentionPeriod { get; }

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

        public int RetryHistoryDepth { get; set; }

        public RemoteInstanceSetting[] RemoteInstances { get; set; }

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

        private string GetAuditLogQueue()
        {
            var value = SettingsReader<string>.Read("ServiceBus", "AuditLogQueue", null);

            if (AuditQueue == null)
            {
                return null;
            }

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
                return null;
            }

            if (value.Equals(Disabled, StringComparison.OrdinalIgnoreCase))
            {
                logger.Info("Audit ingestion disabled.");
                return null; // needs to be null to not create the queues
            }

            return value;
        }

        private string GetErrorQueue()
        {
            var value = SettingsReader<string>.Read("ServiceBus", "ErrorQueue", "error");

            if (value == null)
            {
                logger.Warn("No settings found for error queue to import, if this is not intentional please set add ServiceBus/ErrorQueue to your appSettings");
                return null;
            }

            if (value.Equals(Disabled, StringComparison.OrdinalIgnoreCase))
            {
                logger.Info("Error ingestion disabled.");
                return null; // needs to be null to not create the queues
            }

            return value;
        }

        private string GetErrorLogQueue()
        {
            var value = SettingsReader<string>.Read("ServiceBus", "ErrorLogQueue", null);

            if (ErrorQueue == null)
            {
                return null;
            }

            if (value == null)
            {
                logger.Info("No settings found for error log queue to import, default name will be used");
                return Subscope(ErrorQueue);
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

        private TimeSpan GetEventRetentionPeriod()
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
                if (ValidateConfiguration && result < TimeSpan.FromDays(10))
                {
                    message = "ErrorRetentionPeriod settings is invalid, value should be minimum 10 days.";
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

        static RemoteInstanceSetting[] GetRemoteInstances()
        {
            var valueRead = SettingsReader<string>.Read("RemoteInstances");
            if (!string.IsNullOrEmpty(valueRead))
            {
                var jsonSerializer = JsonSerializer.Create(JsonNetSerializer.CreateDefault());
                using (var jsonReader = new JsonTextReader(new StringReader(valueRead)))
                {
                    return jsonSerializer.Deserialize<RemoteInstanceSetting[]>(jsonReader) ?? new RemoteInstanceSetting[0];
                }
            }

            return new RemoteInstanceSetting[0];
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


        ILog logger = LogManager.GetLogger(typeof(Settings));
        int expirationProcessBatchSize = SettingsReader<int>.Read("ExpirationProcessBatchSize", ExpirationProcessBatchSizeDefault);
        int expirationProcessTimerInSeconds = SettingsReader<int>.Read("ExpirationProcessTimerInSeconds", ExpirationProcessTimerInSecondsDefault);
        int maxBodySizeToStore = SettingsReader<int>.Read("MaxBodySizeToStore", MaxBodySizeToStoreDefault);
        public const string DEFAULT_SERVICE_NAME = "Particular.ServiceControl";
        public const string Disabled = "!disable";

        const int ExpirationProcessTimerInSecondsDefault = 600;
        const int ExpirationProcessBatchSizeDefault = 65512;
        const int ExpirationProcessBatchSizeMinimum = 10240;
        const int MaxBodySizeToStoreDefault = 102400; //100 kb
    }
}