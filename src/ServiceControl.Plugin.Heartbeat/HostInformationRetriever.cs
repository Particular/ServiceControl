namespace ServiceControl.Plugin
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Security.Cryptography;
    using System.Text;
    using NServiceBus;
    using NServiceBus.Unicast;

    static class HostInformationRetriever
    {
        public static HostInformation RetrieveHostInfo()
        {
            //since Hostinfo is available in the core since v4.4 we need to use reflection here
            var hostInformationType = Type.GetType("NServiceBus.Hosting.HostInformation, NServiceBus.Core", false);
            if (hostInformationType == null)
            {
                return GenerateHostinfoForPreV44Endpoints();
            }

            var bus = Configure.Instance.Builder.Build<UnicastBus>();

            var property = typeof(UnicastBus).GetProperty("HostInformation", hostInformationType);

            object hostInfo = property.GetValue(bus, null);

            return new HostInformation
            {
                HostId = (Guid)hostInformationType.GetProperty("HostId").GetValue(hostInfo, null),
                DisplayName = (string)hostInformationType.GetProperty("DisplayName").GetValue(hostInfo, null),
                Properties = (Dictionary<string, string>)hostInformationType.GetProperty("Properties").GetValue(hostInfo, null)
            };
        }

        static HostInformation GenerateHostinfoForPreV44Endpoints()
        {
            var commandLine = Environment.CommandLine;

            var fullPathToStartingExe = commandLine.Split('"')[1];

            var hostId = DeterministicGuid.MakeId(fullPathToStartingExe, Environment.MachineName);

            return new HostInformation
            {
                HostId = hostId,
                DisplayName = String.Format("{0}", fullPathToStartingExe),
                Properties = new Dictionary<string, string>
                {
                    {"Machine", Environment.MachineName},
                    {"ProcessID", Process.GetCurrentProcess().Id.ToString()},
                    {"UserName", Environment.UserName},
                    {"CommandLine", Environment.CommandLine}
                }
            };


        }
    }

    static class DeterministicGuid
    {
        public static Guid MakeId(params string[] data)
        {
            return DeterministicGuidBuilder(String.Concat(data));
        }

        static Guid DeterministicGuidBuilder(string input)
        {
            // use MD5 hash to get a 16-byte hash of the string
            using (var provider = new MD5CryptoServiceProvider())
            {
                var inputBytes = Encoding.Default.GetBytes(input);
                var hashBytes = provider.ComputeHash(inputBytes);
                // generate a guid from the hash:
                return new Guid(hashBytes);
            }
        }
    }


    class HostInformation
    {
        public Guid HostId { get; set; }
        public Dictionary<string, string> Properties { get; set; }
        public string DisplayName { get; set; }
    }
}