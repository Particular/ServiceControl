namespace ServiceControl.Audit.Infrastructure.Settings
{
    using System;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Newtonsoft.Json.Linq;
    using NLog.Common;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using Transports;

    public class Settings
    {
        // Service name is what the user chose when installing the instance or is passing on the command line.
        // We use this as the default endpoint name.
        public static Settings FromConfiguration(string serviceName)
        {
            return new Settings(
                 SettingsReader<string>.Read("InternalQueueName", serviceName) // endpoint name can also be overriden via config
            );
        }

        public Settings(string serviceName, string transportType = null, string persisterType = null)
        {
            ServiceName = serviceName;

            TransportType = transportType ?? SettingsReader<string>.Read("TransportType");

            var persistenceCustomizationType = persisterType ?? SettingsReader<string>.Read("PersistenceType", null);
            var persistenceName = SettingsReader<string>.Read("PersistenceName", null);

            if (persistenceCustomizationType == null && persistenceName == null)
            {
                throw new Exception("No persistence have been configured. Either provide a PeristenceType setting or a PersistenceName setting.");
            }

            //persistenceCustomizationType takes precedence
            if (persistenceCustomizationType == null && persistenceName != null)
            {
                PersistenceName = persistenceName;
                //load the manifest
                var manifestPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Persisters", persistenceName, "persistence.manifest");
                if (!File.Exists(manifestPath))
                {
                    throw new Exception($"Cannot load the manifest file for the configured persister name ({manifestPath})");
                }

                //TODO make this better
                var manifest = JObject.Parse(File.ReadAllText(manifestPath));
                persistenceCustomizationType = manifest["TypeName"].Value<string>();
            }

            PersistenceCustomizationType = persistenceCustomizationType;

            if (string.IsNullOrEmpty(persistenceCustomizationType))
            {
                throw new ConfigurationErrorsException("No persistence have been configured");
            }

            TransportConnectionString = GetConnectionString();

            AuditQueue = GetAuditQueue();
            AuditLogQueue = GetAuditLogQueue(AuditQueue);

            TryLoadLicenseFromConfig();

            ForwardAuditMessages = GetForwardAuditMessages();
            AuditRetentionPeriod = GetAuditRetentionPeriod();
            Port = SettingsReader<int>.Read("Port", 44444);
            MaximumConcurrencyLevel = SettingsReader<int>.Read("MaximumConcurrencyLevel", 32);
            HttpDefaultConnectionLimit = SettingsReader<int>.Read("HttpDefaultConnectionLimit", 100);
            DataSpaceRemainingThreshold = GetDataSpaceRemainingThreshold();
            ServiceControlQueueAddress = SettingsReader<string>.Read("ServiceControlQueueAddress");
            TimeToRestartAuditIngestionAfterFailure = GetTimeToRestartAuditIngestionAfterFailure();
            EnableFullTextSearchOnBodies = SettingsReader<bool>.Read("EnableFullTextSearchOnBodies", true);
        }

        //HINT: acceptance tests only
        public Func<MessageContext, bool> MessageFilter { get; set; }

        public bool ValidateConfiguration => SettingsReader<bool>.Read("ValidateConfig", true);

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

        public string ApiUrl => $"{RootUrl}api";

        public int Port { get; set; }

        public bool PrintMetrics => SettingsReader<bool>.Read("PrintMetrics");
        public string Hostname => SettingsReader<string>.Read("Hostname", "localhost");
        public string VirtualDirectory => SettingsReader<string>.Read("VirtualDirectory", string.Empty);

        public string TransportType { get; private set; }

        public string PersistenceName { get; private set; }

        public string PersistenceCustomizationType { get; private set; }

        public string AuditQueue { get; set; }

        public bool ForwardAuditMessages { get; set; }

        public bool IngestAuditMessages { get; set; } = true;

        public string AuditLogQueue { get; set; }

        public string LicenseFileText { get; set; }

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

        public string ServiceName { get; }

        public int HttpDefaultConnectionLimit { get; set; }
        public string TransportConnectionString { get; set; }
        public int MaximumConcurrencyLevel { get; set; }
        public int DataSpaceRemainingThreshold { get; set; }

        public string ServiceControlQueueAddress { get; set; }

        public TimeSpan TimeToRestartAuditIngestionAfterFailure { get; set; }

        public bool EnableFullTextSearchOnBodies { get; set; }
        public bool ExposeApi { get; set; } = true;

        public TransportCustomization LoadTransportCustomization()
        {
            try
            {
                TransportManifestLibrary.Initialize();

                TransportType = TransportManifestLibrary.Find(TransportType);

                var customizationType = Type.GetType(TransportType, true);

                return (TransportCustomization)Activator.CreateInstance(customizationType);
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load transport customization type {TransportType}.", e);
            }
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

        string GetAuditLogQueue(string auditQueue)
        {
            if (auditQueue == null)
            {
                return null;
            }

            var value = SettingsReader<string>.Read("ServiceBus", "AuditLogQueue", null);

            if (value == null)
            {
                logger.Info("No settings found for audit log queue to import, default name will be used");
                return Subscope(auditQueue);
            }

            return value;
        }

        string GetAuditQueue()
        {
            var value = SettingsReader<string>.Read("ServiceBus", "AuditQueue", "audit");

            if (value.Equals(Disabled, StringComparison.OrdinalIgnoreCase))
            {
                logger.Info("Audit ingestion disabled.");
                IngestAuditMessages = false;
                return null; // needs to be null to not create the queues
            }

            return value;
        }

        static bool GetForwardAuditMessages()
        {
            var forwardAuditMessages = NullableSettingsReader<bool>.Read("ForwardAuditMessages");
            if (forwardAuditMessages.HasValue)
            {
                return forwardAuditMessages.Value;
            }

            return false;
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

        TimeSpan GetAuditRetentionPeriod()
        {
            string message;
            var valueRead = SettingsReader<string>.Read("AuditRetentionPeriod");
            if (valueRead == null)
            {
                //same default as SCMU
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
        int maxBodySizeToStore = SettingsReader<int>.Read("MaxBodySizeToStore", MaxBodySizeToStoreDefault);
        public const string DEFAULT_SERVICE_NAME = "Particular.ServiceControl.Audit";
        public const string Disabled = "!disable";

        const int MaxBodySizeToStoreDefault = 102400; //100 kb
        const int DataSpaceRemainingThresholdDefault = 20;
    }
}