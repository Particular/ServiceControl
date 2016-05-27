namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;
    using System.IO;
    using NLog;
    using NLog.Common;

    public static class LoggingSettings
    {
        public static string ServiceName;

        public static LogLevel LoggingLevel
        {
            get
            {
                var level = LogLevel.Warn;
                try
                {
                    level = LogLevel.FromString(SettingsReader<string>.Read("LogLevel", LogLevel.Warn.Name));
                }
                catch
                {
                    InternalLogger.Warn("Failed to parse LogLevel setting. Defaulting to Warn");
                }
                return level;
            }
        }

        public static LogLevel RavenDBLogLevel
        {
            get
            {
                var level = LogLevel.Warn;
                try
                {
                    level = LogLevel.FromString(SettingsReader<string>.Read("RavenDBLogLevel", LogLevel.Warn.Name));
                }
                catch
                {
                    InternalLogger.Warn("Failed to parse RavenDBLogLevel setting. Defaulting to Warn");
                }
                return level;
            }
        }


        public static string LogPath => Environment.ExpandEnvironmentVariables(SettingsReader<string>.Read("LogPath", DefaultLogPathForInstance()));

        private static string DefaultLogPathForInstance()
        {
            if (ServiceName.Equals("Particular.ServiceControl", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Particular\\ServiceControl\\logs");
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), $"Particular\\{Settings.ServiceName}\\logs");
        }
    }
}