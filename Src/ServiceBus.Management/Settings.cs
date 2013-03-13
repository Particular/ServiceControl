namespace ServiceBus.Management
{
    using System;
    using System.Configuration;
    using System.IO;
    using NServiceBus;
    using NServiceBus.Logging;
    using Satellites;

    public class Settings
    {

        public static int Port
        {
            get
            {
                if (port == 0)
                    port = SettingsReader<int>.Read("Port", 8888);

                return port;
            }

        }

        public static string Hostname
        {
            get
            {
                if (hostname == null)
                    hostname = SettingsReader<string>.Read("Hostname", "localhost");

                return hostname;
            }

        }

        public static string VirtualDirectory
        {
            get
            {
                if (virtualDirectory == null)
                {
                    virtualDirectory = SettingsReader<string>.Read("VirtualDirectory", "");
                }

                return virtualDirectory;
            }
        }

        public static string ApiUrl
        {
            get
            {
                var vdir = VirtualDirectory;

                if (!string.IsNullOrEmpty(vdir))
                    vdir += "/";

                vdir += "api";

                var url = string.Format("http://{0}:{1}/{2}", Hostname, Port, vdir);

                if (!url.EndsWith("/"))
                    url += "/";

                return url;
            }
        }


        public static Address AuditQueue
        {
            get
            {
                if (auditQueue == null)
                {
                    var value = SettingsReader<string>.Read("NServiceBus", "AuditQueue", null);

                    if (value != null)
                        auditQueue = Address.Parse(value);
                    else
                    {
                        Logger.Warn("No settings found for audit queue to import, if this is not intentional please set add NServiceBus/AuditQueue to your appSettings");

                        auditQueue = Address.Undefined;
                    }


                }


                return auditQueue;
            }
        }

        public static Address ErrorQueue
        {
            get
            {
                if (errorQueue == null)
                {
                    var value = SettingsReader<string>.Read("NServiceBus", "ErrorQueue", null);

                    if (value != null)
                        errorQueue = Address.Parse(value);
                    else
                    {
                        Logger.Warn("No settings found for error queue to import, if this is not intentional please set add NServiceBus/ErrorQueue to your appSettings");

                        errorQueue = Address.Undefined;
                    }


                }


                return errorQueue;
            }
        }

        public static string DbPath
        {
            get
            {
                if (dbPath == null)
                    dbPath = SettingsReader<string>.Read("DbPath", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Particular", "ServiceBus.Management"));

                return dbPath;
            }

        }

        static int port;
        static string hostname;
        static string virtualDirectory;
        static string dbPath;
        static Address auditQueue;
        static Address errorQueue;
        static readonly ILog Logger = LogManager.GetLogger(typeof(Settings));
    }
}