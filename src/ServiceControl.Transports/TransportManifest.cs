namespace ServiceControl.Transports
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using NServiceBus.Logging;

    public class TransportManifest
    {
        public string Version { get; set; }

        public string Location { get; set; }

        public TransportManifestDefinition[] Definitions { get; set; }
    }

    public class TransportManifestDefinition
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string TypeName { get; set; }

        public string[] Aliases { get; set; }

        internal bool IsMatch(string transportType) =>
            string.Compare(TypeName, transportType, false) == 0 // Type names are case sensitive
            || string.Compare(Name, transportType, true) == 0
            || AliasesContain(transportType);

        bool AliasesContain(string transportType)
        {
            if (Aliases == null)
            {
                return false;
            }

            return Aliases.Contains(transportType);
        }
    }

    public static class TransportManifestLibrary
    {
        public static List<TransportManifest> TransportManifests { get; } = new List<TransportManifest>();

        static TransportManifestLibrary()
        {
            var assemblyDirectory = GetAssemblyDirectory();

            try
            {
                foreach (var manifestFile in Directory.EnumerateFiles(assemblyDirectory, "transport.manifest", SearchOption.AllDirectories))
                {
                    var manifest = JsonSerializer.Deserialize<TransportManifest>(File.ReadAllText(manifestFile));
                    manifest.Location = Path.GetDirectoryName(manifestFile);

                    TransportManifests.Add(manifest);
                }
            }
            catch (Exception ex)
            {
                logger.Warn($"Failed to load transport manifests from {assemblyDirectory}", ex);
            }

            try
            {
                foreach (var manifestFile in DevelopmentTransportLocations.ManifestFiles)
                {
                    var manifest = JsonSerializer.Deserialize<TransportManifest>(File.ReadAllText(manifestFile));
                    manifest.Location = Path.GetDirectoryName(manifestFile);

                    TransportManifests.Add(manifest);
                }
            }
            catch (Exception ex)
            {
                logger.Warn($"Failed to load transport manifests from development locations", ex);
            }

            TransportManifests.SelectMany(t => t.Definitions).ToList().ForEach(m => logger.Info($"Found transport manifest for {m.DisplayName}"));
        }

        static string GetAssemblyDirectory()
        {
            var assemblyLocation = typeof(TransportManifestLibrary).Assembly.Location;
            return Path.GetDirectoryName(assemblyLocation);
        }

        public static string Find(string transportType)
        {
            if (transportType == null)
            {
                throw new Exception("No transport has been configured. Either provide a Type or Name in the TransportType setting.");
            }

            var transportManifestDefinition = TransportManifests
                .SelectMany(t => t.Definitions)
                .FirstOrDefault(w => w.IsMatch(transportType));

            return transportManifestDefinition?.TypeName ?? transportType;
        }

        public static string GetTransportFolder(string transportType)
        {
            if (transportType == null)
            {
                throw new Exception("No transport has been configured. Either provide a Type or Name in the TransportType setting.");
            }

            string transportFolder = null;

            foreach (var manifest in TransportManifests)
            {
                var match = false;

                foreach (var definition in manifest.Definitions)
                {
                    if (definition.IsMatch(transportType))
                    {
                        match = true;
                        break;
                    }
                }

                if (match)
                {
                    transportFolder = manifest.Location;
                    break;
                }
            }

            return transportFolder;
        }

        static readonly ILog logger = LogManager.GetLogger(typeof(TransportManifestLibrary));
    }
}


