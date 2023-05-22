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
        static void Initialize()
        {
            if (TransportManifests == null)
            {
                TransportManifests = new List<TransportManifest>();
            }

            if (!initialized && Assembly.GetEntryAssembly() != null)
            {
                initialized = true;
                var assemblyLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                Directory.EnumerateFiles(assemblyLocation, "transport.manifest", SearchOption.AllDirectories).ToList().ForEach(manifest =>
                {
                    TransportManifests.Add(System.Text.Json.JsonSerializer.Deserialize<TransportManifest>(File.ReadAllText(manifest)));
                });
            }
        }

        public static string Find(string transportType)
        {
            if (transportType == null)
            {
                throw new Exception("No transport has been configured. Either provide a Type or Name in the TransportType setting.");
            }

            Initialize();

            var transportManifestDefinition = TransportManifests.SelectMany(t => t.Definitions).Where(w =>
                    string.Compare(w.TypeName, transportType, true) == 0 || string.Compare(w.Name, transportType, true) == 0
                    || w.Aliases.Contains(transportType)).FirstOrDefault();

            if (transportManifestDefinition != null)
            {
                return transportManifestDefinition.TypeName;
            }

            return transportType;
        }

        public static string GetTransportFolder(string transportType)
        {
            if (transportType == null)
            {
                throw new Exception("No transport has been configured. Either provide a Type or Name in the TransportType setting.");
            }

            Initialize();

            var transportManifestDefinition = TransportManifests.SelectMany(t => t.Definitions).Where(w =>
                    string.Compare(w.TypeName, transportType, true) == 0 || string.Compare(w.Name, transportType, true) == 0
                    || w.Aliases.Contains(transportType)).FirstOrDefault();

            if (transportManifestDefinition != null)
            {
                return transportManifestDefinition.Name.Split('.').FirstOrDefault();
            }

            return null;
        }
    }
}


