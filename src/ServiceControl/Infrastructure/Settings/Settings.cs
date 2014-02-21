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
        }

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

                return string.Format("{0}://{1}:{2}/{3}", Schema, Hostname, Port, suffix);
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

                return string.Format("{0}://{1}:{2}/{3}", Schema, Hostname, Port, suffix);
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
            var value = SettingsReader<string>.Read("ServiceBus", "AuditQueue", null);

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
            var value = SettingsReader<string>.Read("ServiceBus", "ErrorQueue", null);

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

            var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Particular", "ServiceControl", dbFolder);

            return SettingsReader<string>.Read("DbPath", defaultPath);
        }


        static string SanitiseFolderName(string folderName)
        {
            return Path.GetInvalidPathChars().Aggregate(folderName, (current, c) => current.Replace(c, '-'));
        }

        public static int Port = SettingsReader<int>.Read("Port", 33333);
        public static bool ExposeRavenDB = SettingsReader<bool>.Read("ExposeRavenDB");
        public static string Schema = SettingsReader<string>.Read("Schema", "http");
        public static string Hostname = SettingsReader<string>.Read("Hostname", "localhost");
        public static string VirtualDirectory = SettingsReader<string>.Read("VirtualDirectory", String.Empty);

        public static string LogPath =
            Environment.ExpandEnvironmentVariables(SettingsReader<string>.Read("LogPath",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Particular\\ServiceControl\\logs")));

        public static string DbPath;
        public static Address ErrorLogQueue;
        public static Address ErrorQueue;
        public static Address AuditQueue;
        public static bool ForwardAuditMessages = SettingsReader<bool>.Read("ForwardAuditMessages");
        public static bool CreateIndexSync = SettingsReader<bool>.Read("CreateIndexSync");
        public static Address AuditLogQueue;

        public static int ExpirationProcessTimerInSeconds = SettingsReader<int>.Read("ExpirationProcessTimerInSeconds", 60); // default is once a minute
        public static int HoursToKeepMessagesBeforeExpiring = SettingsReader<int>.Read("HoursToKeepMessagesBeforeExpiring", 24 * 30); // default is 30 days

        static readonly ILog Logger = LogManager.GetLogger(typeof(Settings));
    }
}