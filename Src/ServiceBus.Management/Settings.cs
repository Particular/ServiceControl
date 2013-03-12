namespace ServiceBus.Management
{
    using System;
    using System.IO;

    public class Settings
    {
        static int port;
        static string hostname;
        static string virtualDirectory;
        static string dbPath;

        public static int Port
        {
            get
            {
                if(port == 0)
                    port = SettingsReader<int>.Read("Port",8888);
                
                return port;
            }
            
        }

        public static string Hostname
        {
            get
            {
                if (hostname == null)
                    hostname = SettingsReader<string>.Read("Hostname","localhost");

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

                    if (virtualDirectory.StartsWith("/"))
                        virtualDirectory = virtualDirectory.Substring(1);
                }
                    
                return virtualDirectory;
            }
        }

        public static string ApiUrl
        {
            get
            {
                var url = string.Format("http://{0}:{1}/{2}",Hostname,Port,VirtualDirectory);

                if (!url.EndsWith("/"))
                    url += "/";

                return url;
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
    }
}