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
        public TransportManifestDefinition[] Definitions { get; set; }

        public override string ToString() => $"{nameof(TransportManifest)}: {string.Join(", ", Definitions.Select(d => d.Name))}";
    }

    public class TransportManifestDefinition
    {
        public string Location { get; set; }

        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string AssemblyName { get; set; }

        public string TypeName { get; set; }

        public string[] Aliases { get; set; } = [];

        internal bool IsMatch(string transportType) =>
            string.Equals(TypeName, transportType, StringComparison.Ordinal) // Type names are case-sensitive
            || string.Equals(Name, transportType, StringComparison.OrdinalIgnoreCase)
            || Aliases.Contains(transportType, StringComparer.Ordinal);

        public override string ToString() => $"{nameof(TransportManifestDefinition)}: {Name}";
    }

    public static class TransportManifestLibrary
    {
        public static List<TransportManifest> TransportManifests { get; } = [];

        static TransportManifestLibrary()
        {
            var assemblyDirectory = GetAssemblyDirectory();

            try
            {
                foreach (var manifestFile in Directory.EnumerateFiles(assemblyDirectory, "transport.manifest", SearchOption.AllDirectories))
                {
                    var manifest = JsonSerializer.Deserialize<TransportManifest>(File.ReadAllText(manifestFile));

                    foreach (var definition in manifest.Definitions)
                    {
                        definition.Location = Path.GetDirectoryName(manifestFile);
                    }

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

                    foreach (var definition in manifest.Definitions)
                    {
                        definition.Location = Path.GetDirectoryName(manifestFile);
                    }

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

        public static TransportManifestDefinition Find(string transportType)
        {
            if (transportType == null)
            {
                throw new Exception("No transport has been configured. Either provide a Type or Name in the TransportType setting.");
            }

            var transportManifestDefinition = TransportManifests
                .SelectMany(t => t.Definitions)
                .FirstOrDefault(w => w.IsMatch(transportType));

            return transportManifestDefinition;
        }

        static readonly ILog logger = LogManager.GetLogger(typeof(TransportManifestLibrary));
    }
}