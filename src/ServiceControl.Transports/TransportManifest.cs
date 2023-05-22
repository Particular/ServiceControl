namespace ServiceControl.Transports
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    public class TransportManifest
    {
        public string Version { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string TypeName { get; set; }
        public List<TransportManifestDefinition> Definitions { get; set; }
    }

    public class TransportManifestDefinition
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string TypeName { get; set; }
        public List<string> Aliases { get; set; }
    }

    public static class TransportManifestLibrary
    {
        public static List<TransportManifest> TransportManifests { get; set; }

        static bool initialized;
        public static void Initialize()
        {
            if (!initialized)
            {
                initialized = true;
                var assemblyLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                Directory.EnumerateFiles(assemblyLocation, "transport.manifest", SearchOption.AllDirectories).ToList().ForEach(manifest =>
                {
                    if (TransportManifests == null)
                    {
                        TransportManifests = new List<TransportManifest>();
                    }

                    TransportManifests.Add(System.Text.Json.JsonSerializer.Deserialize<TransportManifest>(File.ReadAllText(manifest)));
                });
            }
        }

        public static string Find(string transportType, string transportName)
        {
            if (transportType == null && transportName == null)
            {
                throw new Exception("No transport have been configured. Either provide a TransportType setting or a TransportName setting.");
            }

            if (transportType != null)
            {
                var transportManifestDefinition = TransportManifests.SelectMany(t => t.Definitions).Where(w =>
                    string.Compare(w.TypeName, transportType, true) == 0
                    || w.Aliases.Contains(transportType)).FirstOrDefault();
                if (transportManifestDefinition != null)
                {
                    return transportManifestDefinition.TypeName;
                }
            }
            else
            {
                var transportManifestDefinition = TransportManifests.SelectMany(t => t.Definitions).Where(w => string.Compare(w.Name, transportName, true) == 0).FirstOrDefault();
                if (transportManifestDefinition != null)
                {
                    return transportManifestDefinition.TypeName;
                }
            }

            return transportType;
        }
    }
}


