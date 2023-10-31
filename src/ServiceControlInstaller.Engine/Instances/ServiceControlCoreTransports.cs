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
        public static readonly TransportInfo[] All;

        static ServiceControlCoreTransports()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var list = new List<PersistenceManifest>();

            var resourceNames = assembly.GetManifestResourceNames()
                .Where(name => name.EndsWith("transport.manifest"))
                .ToArray();

            All = resourceNames
                .SelectMany(name => Load(assembly, name))
                .OrderBy(m => m.Name)
                .ToArray();

            // Filtered by environment variable in SCMU, needs to be available all the time for PowerShell
            Find("LearningTransport").AvailableInSCMU = IncludeLearningTransport();
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

        public static IEnumerable<string> GetTransportNames(bool includeDisplayNames)
        {
            foreach (var transport in All)
            {
                if (transport.AvailableInSCMU)
                {
                    yield return transport.Name;
                    if (includeDisplayNames)
                    {
                        yield return transport.DisplayName;
                    }
                }
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
            return All.FirstOrDefault(p => p.Matches(name));
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
