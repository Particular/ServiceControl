﻿namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;
    using System.IO;
    using System.Linq;
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
            TransportType = SettingsReader<string>.Read("TransportType", typeof(Msmq).AssemblyQualifiedName);
        }

        public static bool MaintenanceMode;

        public static string ApiUrl
        {
            get
            {
                var suffix = VirtualDirectory;

                if (!string.IsNullOrEmpty(suffix))
                {
                    suffix += "/";
                }

                suffix += "api/";

                return string.Format("http://{0}:{1}/{2}", Hostname, Port, suffix);
            }
        }

        public static string StorageUrl
        {
            get
            {
                var suffix = VirtualDirectory;

                if (!string.IsNullOrEmpty(suffix))
                {
                    suffix += "/";
                }

                suffix += "storage/";

                return string.Format("http://{0}:{1}/{2}", Hostname, Port, suffix);
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
            var dbFolder = String.Format("{0}-{1}", host, Port);

            if (!string.IsNullOrEmpty(VirtualDirectory))
            {
                dbFolder += String.Format("-{0}", SanitiseFolderName(VirtualDirectory));
            }

            var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Particular", "ServiceControl", dbFolder);

            return SettingsReader<string>.Read("DbPath", defaultPath);
        }

        static string SanitiseFolderName(string folderName)
        {
            return Path.GetInvalidPathChars().Aggregate(folderName, (current, c) => current.Replace(c, '-'));
        }

        public static int Port = SettingsReader<int>.Read("Port", 33333);
      
        public static bool ExposeRavenDB = SettingsReader<bool>.Read("ExposeRavenDB");
        public static string Hostname = SettingsReader<string>.Read("Hostname", "localhost");
        public static string VirtualDirectory = SettingsReader<string>.Read("VirtualDirectory", String.Empty);
        public static TimeSpan HeartbeatGracePeriod = TimeSpan.Parse(SettingsReader<string>.Read("HeartbeatGracePeriod", "00:00:40"));
        public static string TransportType { get; set; }

        public static string LogPath
        {
            get
            {
                return Environment.ExpandEnvironmentVariables(SettingsReader<string>.Read("LogPath", DefaultLogPathForInstance())); 
            }
        }

        public static int ExternalIntegrationsDispatchingBatchSize = SettingsReader<int>.Read("ExternalIntegrationsDispatchingBatchSize", 100);

        public static int MaximumMessageThroughputPerSecond = SettingsReader<int>.Read("MaximumMessageThroughputPerSecond", 350);

        public static string DbPath;
        public static Address ErrorLogQueue;
        public static Address ErrorQueue;
        public static Address AuditQueue;
        public static bool? ForwardAuditMessages = NullableSettingsReader<bool>.Read("ForwardAuditMessages");
        public static bool CreateIndexSync = SettingsReader<bool>.Read("CreateIndexSync");
        public static Address AuditLogQueue;
        
        const int ExpirationProcessTimerInSecondsDefault = 600;
        static int expirationProcessTimerInSeconds = SettingsReader<int>.Read("ExpirationProcessTimerInSeconds", ExpirationProcessTimerInSecondsDefault); 
        public static int ExpirationProcessTimerInSeconds
        {
            get
            {
                if ((expirationProcessTimerInSeconds < 0) || (expirationProcessTimerInSeconds > TimeSpan.FromHours(3).TotalSeconds))
                {
                    Logger.ErrorFormat("ExpirationProcessTimerInSeconds settings is invalid, the valid range is 0 to {0}. Defaulting to {1}" , TimeSpan.FromHours(3).TotalSeconds, ExpirationProcessTimerInSecondsDefault);
                    return ExpirationProcessTimerInSecondsDefault;
                }
                return expirationProcessTimerInSeconds;
            }
            set { expirationProcessTimerInSeconds = value; }
        }
        
        const int HoursToKeepMessagesBeforeExpiringDefault = 720; 
        static int hoursToKeepMessagesBeforeExpiring = SettingsReader<int>.Read("HoursToKeepMessagesBeforeExpiring", HoursToKeepMessagesBeforeExpiringDefault);
        public static int HoursToKeepMessagesBeforeExpiring
        {
            get
            {
                if ((hoursToKeepMessagesBeforeExpiring < 24) || (hoursToKeepMessagesBeforeExpiring > 1440))
                {
                    Logger.ErrorFormat("HoursToKeepMessagesBeforeExpiring settings is invalid, the valid range is 24 to 1440 (60 days).  Defaulting to {0}",  HoursToKeepMessagesBeforeExpiringDefault);
                    return HoursToKeepMessagesBeforeExpiringDefault;
                }
                return hoursToKeepMessagesBeforeExpiring;
            }
            set { hoursToKeepMessagesBeforeExpiring = value; }
        }

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
            set { expirationProcessBatchSize = value; }
        }

      
        static readonly ILog Logger = LogManager.GetLogger(typeof(Settings));
        public static string ServiceName;

        static string DefaultLogPathForInstance()
        {
            if (ServiceName.Equals("Particular.ServiceControl", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Particular\\ServiceControl\\logs");
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), string.Format("Particular\\{0}\\logs", ServiceName));
        }
    }
}