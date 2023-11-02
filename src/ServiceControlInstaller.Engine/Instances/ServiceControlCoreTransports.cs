﻿namespace ServiceControlInstaller.Engine.Instances
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

        public static TransportInfo[] GetSupportedTransports() => supported;

        // Only tests should use this
        internal static TransportInfo[] GetAllTransports() => all;

        public static IEnumerable<T> Select<T>(Func<TransportInfo, T> selector) => all.Select(selector);

        static ServiceControlCoreTransports()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var list = new List<PersistenceManifest>();

            var resourceNames = assembly.GetManifestResourceNames()
                .Where(name => name.EndsWith("transport.manifest"))
                .ToArray();

            all = resourceNames
                .SelectMany(name => Load(assembly, name))
                .OrderBy(m => m.Name)
                .ToArray();

            // Filtered by environment variable in SCMU, needs to be available all the time for PowerShell
            all.First(t => t.Name == "LearningTransport").AvailableInSCMU = IncludeLearningTransport();

            supported = all.Where(t => t.AvailableInSCMU).ToArray();
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
            foreach (var transport in all)
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
            if (string.IsNullOrEmpty(name))
            {
                name = "MSMQ";
            }

            return all.FirstOrDefault(p => p.Matches(name)) ?? new TransportInfo
            {
                Name = name,
                DisplayName = $"Unknown Message Transport: {name}",
                AvailableInSCMU = false
            };
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
