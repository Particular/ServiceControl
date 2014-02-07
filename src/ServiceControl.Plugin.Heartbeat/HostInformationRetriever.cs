namespace ServiceControl.Plugin
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Security.Cryptography;
    using System.Text;

    static class HostInformationRetriever
    {
        public static HostInformation RetrieveHostInfo()
        {
            //since Hostinfo is available in the core since v4.4 we need to use reflaction here
            var hostInformationType = Type.GetType("NServiceBus.Hosting.HostInformation, NServiceBus.Core", false);
            if (hostInformationType == null)
            {
                return GenerateHostinfoForPreV44Endpoints();
            }

            return new HostInformation
            {
                HostId = (string)hostInformationType.GetProperty("HostId").GetValue(null, null),
                Properties = (Dictionary<string, string>)hostInformationType.GetProperty("Properties").GetValue(null, null)
            };
        }

        static HostInformation GenerateHostinfoForPreV44Endpoints()
        {
            var commandLine = Environment.CommandLine;

            var fullPathToStartingExe = commandLine.Split('"')[1];

            var hostId = DeterministicGuid.MakeId(fullPathToStartingExe, Environment.MachineName);

            return new HostInformation
            {
                HostId = hostId.ToString(),
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
        public string HostId { get; set; }
        public Dictionary<string, string> Properties { get; set; }
        public string DisplayName { get; set; }
    }
}