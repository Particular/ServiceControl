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

            var includeLearning = IncludeLearningTransport();

            All = resourceNames
                .SelectMany(name => Load(assembly, name))
                .Where(manifest => includeLearning || manifest.Name != "LearningTransport")
                .OrderBy(m => m.Name)
                .ToArray();
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
