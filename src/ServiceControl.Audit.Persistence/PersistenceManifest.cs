namespace ServiceControl.Audit.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using Microsoft.Extensions.Logging;
    using ServiceControl.Infrastructure;

    public class PersistenceManifest
    {
        public string Location { get; set; }

        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string Description { get; set; }

        public string AssemblyName { get; set; }

        public string TypeName { get; set; }

        public bool IsSupported { get; set; } = true;

        public string[] Aliases { get; set; } = [];

        internal bool IsMatch(string persistenceType) =>
            string.Equals(TypeName, persistenceType, StringComparison.Ordinal) // Type names are case-sensitive
            || string.Equals(Name, persistenceType, StringComparison.OrdinalIgnoreCase)
            || Aliases.Contains(persistenceType, StringComparer.Ordinal);
    }

    public static class PersistenceManifestLibrary
    {
        public static List<PersistenceManifest> PersistenceManifests { get; } = [];

        static PersistenceManifestLibrary()
        {
            var assemblyDirectory = GetAssemblyDirectory();

            try
            {
                PersistenceManifests.AddRange(LoadManifests(Directory.EnumerateFiles(assemblyDirectory, "persistence.manifest", SearchOption.AllDirectories)));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load persistence manifests from {AssemblyDirectory}", assemblyDirectory);
            }

            try
            {
                PersistenceManifests.AddRange(LoadManifests(DevelopmentPersistenceLocations.ManifestFiles));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load persistence manifests from development locations");
            }

            PersistenceManifests.ForEach(m => logger.LogInformation("Found persistence manifest for {ManifestDisplayName}", m.DisplayName));
        }

        // One unreadable manifest must not hide the persisters enumerated after it
        internal static List<PersistenceManifest> LoadManifests(IEnumerable<string> manifestFiles)
        {
            var manifests = new List<PersistenceManifest>();

            foreach (var manifestFile in manifestFiles)
            {
                try
                {
                    manifests.Add(DeserializeManifest(manifestFile));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to load persistence manifest {ManifestFile}", manifestFile);
                }
            }

            return manifests;
        }

        static PersistenceManifest DeserializeManifest(string manifestFile)
        {
            var manifest = JsonSerializer.Deserialize<PersistenceManifest>(File.ReadAllText(manifestFile))
                ?? throw new InvalidDataException($"The persistence manifest '{manifestFile}' is empty or invalid.");
            manifest.Location = Path.GetDirectoryName(manifestFile);
            return manifest;
        }

        static string GetAssemblyDirectory()
        {
            var assemblyLocation = typeof(PersistenceManifestLibrary).Assembly.Location;
            return Path.GetDirectoryName(assemblyLocation);
        }

        public static PersistenceManifest Find(string persistenceType)
        {
            if (persistenceType == null)
            {
                throw new Exception("No persistenceType has been configured. Either provide a Type or Name in the PersistenceType setting.");
            }

            var persistenceManifest = PersistenceManifests.FirstOrDefault(w => w.IsMatch(persistenceType));

            return persistenceManifest;
        }

        static readonly ILogger logger = LoggerUtil.CreateStaticLogger(typeof(PersistenceManifestLibrary));
    }
}


