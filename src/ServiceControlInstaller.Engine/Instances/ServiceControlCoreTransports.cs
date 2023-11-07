namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Newtonsoft.Json;

    public static class ServiceControlCoreTransports
    {
        static readonly TransportInfo[] all;
        static readonly TransportInfo[] supported;
        static readonly TransportInfo defaultTransport;

        public static TransportInfo[] GetSupportedTransports() => supported;
        public static TransportInfo GetDefaultTransport() => defaultTransport;

        // Only tests should use this
        internal static TransportInfo[] GetAllTransports() => all;

        static ServiceControlCoreTransports()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var list = new List<PersistenceManifest>();

            var resourceNames = assembly.GetManifestResourceNames()
                .Where(name => name.EndsWith("transport.manifest"))
                .ToArray();

            all = resourceNames
                .SelectMany(name => Load(assembly, name))
                .OrderBy(m => m.DisplayName)
                .ToArray();

            // Filtered by environment variable in SCMU, needs to be available all the time for PowerShell
            all.First(t => t.Name == "LearningTransport").AvailableInSCMU = IncludeLearningTransport();

            supported = all.Where(t => t.AvailableInSCMU).ToArray();

            defaultTransport = all.Single(t => t.Default);
        }

        static TransportInfo[] Load(Assembly assembly, string resourceName)
        {
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            using (var reader = new StreamReader(stream))
            {
                var manifestContent = reader.ReadToEnd();
                var manifest = JsonConvert.DeserializeObject<TransportManifest>(manifestContent);

                return manifest.Definitions;
            }
        }

        static bool IncludeLearningTransport()
        {
            try
            {
                var environmentValue = Environment.GetEnvironmentVariable("ServiceControl_IncludeLearningTransport");

                if (environmentValue != null)
                {
                    environmentValue = Environment.ExpandEnvironmentVariables(environmentValue);
                    if (bool.TryParse(environmentValue, out var enabled))
                    {
                        return enabled;
                    }
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }


        public static TransportInfo Find(string name)
        {
            return all.FirstOrDefault(p => p.Matches(name));
        }

        public static TransportInfo UpgradedTransportSeam(TransportInfo transport)
        {
            if (!string.IsNullOrWhiteSpace(transport.AutoMigrateTo))
            {
                return Find(transport.AutoMigrateTo);
            }

            return transport;
        }

    }
}
