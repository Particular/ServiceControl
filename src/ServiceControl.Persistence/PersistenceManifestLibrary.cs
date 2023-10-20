namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using NServiceBus.Logging;

    public class PersistenceManifest
    {
        public string Version { get; set; }

        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string Description { get; set; }

        public string TypeName { get; set; }

        public bool IsSupported { get; set; } = true;

        internal bool IsMatch(string persistenceType) =>
            string.Compare(TypeName, persistenceType, true) == 0
            || string.Compare(Name, persistenceType, true) == 0;
    }

    public static class PersistenceManifestLibrary
    {
        public static List<PersistenceManifest> PersistenceManifests { get; set; }

        static bool initialized;

        static bool usingDevelopmentLocation;

        static void Initialize(string persistenceType)
        {
            if (PersistenceManifests == null)
            {
                PersistenceManifests = new List<PersistenceManifest>();
            }

            if (!initialized)
            {
                initialized = true;
                var persistenceFolder = GetAssemblyDirectory();

                try
                {
                    PersistenceManifests.AddRange(
                        Directory.EnumerateFiles(persistenceFolder, "persistence.manifest", SearchOption.AllDirectories)
                        .Select(manifest => JsonSerializer.Deserialize<PersistenceManifest>(File.ReadAllText(manifest)))
                    );

                    if (PersistenceManifests.Count == 0 && DevelopmentPersistenceLocations.TryGetPersistenceFolder(persistenceType, out persistenceFolder))
                    {
                        var manifest = Path.Combine(persistenceFolder, "persistence.manifest");
                        PersistenceManifests.Add(JsonSerializer.Deserialize<PersistenceManifest>(File.ReadAllText(manifest)));

                        usingDevelopmentLocation = true;
                    }

                    PersistenceManifests.ForEach(m => logger.Info($"Found persistence manifest for {m.DisplayName}"));
                }
                catch (Exception ex)
                {
                    logger.Warn($"Failed to load persistence manifests from {persistenceFolder}", ex);
                }
            }
        }

        static string GetAssemblyDirectory()
        {
            var assemblyLocation = typeof(PersistenceManifestLibrary).Assembly.Location;
            return Path.GetDirectoryName(assemblyLocation);
        }

        public static string Find(string persistenceType)
        {
            if (persistenceType == null)
            {
                throw new Exception("No persistenceType has been configured. Either provide a Type or Name in the PersistenceType setting.");
            }

            Initialize(persistenceType);

            var persistenceManifestDefinition = PersistenceManifests.FirstOrDefault(w => w.IsMatch(persistenceType));

            if (persistenceManifestDefinition != null)
            {
                return persistenceManifestDefinition.TypeName;
            }

            return persistenceType;
        }

        public static string GetPersistenceFolder(string persistenceType)
        {
            if (persistenceType == null)
            {
                throw new Exception("No persistenceType has been configured. Either provide a Type or Name in the PersistenceType setting.");
            }

            Initialize(persistenceType);

            var persistenceManifestDefinition = PersistenceManifests.FirstOrDefault(w => w.IsMatch(persistenceType));

            string persistenceFolder = null;

            if (persistenceManifestDefinition != null)
            {
                if (usingDevelopmentLocation)
                {
                    _ = DevelopmentPersistenceLocations.TryGetPersistenceFolder(persistenceManifestDefinition.Name, out persistenceFolder);
                }
                else
                {
                    var appDirectory = GetAssemblyDirectory();
                    var persistenceName = persistenceManifestDefinition.Name.Split('.').FirstOrDefault();
                    persistenceFolder = Path.Combine(appDirectory, "Persisters", persistenceName);
                }
            }

            return persistenceFolder;
        }

        static readonly ILog logger = LogManager.GetLogger(typeof(PersistenceManifestLibrary));
    }
}

