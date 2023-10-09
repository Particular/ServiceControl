namespace ServiceControl.Audit.Persistence
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
        static void Initialize()
        {
            if (PersistenceManifests == null)
            {
                PersistenceManifests = new List<PersistenceManifest>();
            }

            if (!initialized)
            {
                initialized = true;
                var assemblyLocation = GetAssemblyDirectory();
                try
                {
                    PersistenceManifests.AddRange(
                        Directory.EnumerateFiles(assemblyLocation, "persistence.manifest", SearchOption.AllDirectories)
                        .Select(manifest => JsonSerializer.Deserialize<PersistenceManifest>(File.ReadAllText(manifest)))
                        );

                    PersistenceManifests.ForEach(m => logger.Info($"Found persistence manifest for {m.DisplayName}"));
                }
                catch (Exception ex)
                {
                    logger.Warn($"Failed to load persistence manifests from {assemblyLocation}", ex);
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

            Initialize();

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

            Initialize();

            var persistenceManifestDefinition = PersistenceManifests.FirstOrDefault(w => w.IsMatch(persistenceType));

            if (persistenceManifestDefinition != null)
            {
                return persistenceManifestDefinition.Name.Split('.').FirstOrDefault();
            }

            return null;
        }

        static ILog logger = LogManager.GetLogger(typeof(PersistenceManifestLibrary));
    }
}


