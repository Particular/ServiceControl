namespace ServiceControl.Audit.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    class PersistenceManifest
    {
        public string Version { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string TypeName { get; set; }
    }

    static class PersistenceManifestLibrary
    {
        public static List<PersistenceManifest> PersistenceManifests { get; set; }

        static bool initialized;
        public static void Initialize()
        {
            if (!initialized)
            {
                initialized = true;
                var assemblyLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                Directory.EnumerateFiles(assemblyLocation, "persistence.manifest", SearchOption.AllDirectories).ToList().ForEach(manifest =>
                {
                    if (PersistenceManifests == null)
                    {
                        PersistenceManifests = new List<PersistenceManifest>();
                    }

                    PersistenceManifests.Add(System.Text.Json.JsonSerializer.Deserialize<PersistenceManifest>(File.ReadAllText(manifest)));
                });
            }
        }

        public static string Find(string persistenceType)
        {
            if (persistenceType == null)
            {
                throw new Exception("No persistenceType has been configured. Either provide a Type or Name in the PersistenceType setting.");
            }

            var persistenceManifestDefinition = PersistenceManifests.Where(w =>
                    string.Compare(w.TypeName, persistenceType, true) == 0 || string.Compare(w.Name, persistenceType, true) == 0).FirstOrDefault();

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

            var persistenceManifestDefinition = PersistenceManifests.Where(w =>
                    string.Compare(w.TypeName, persistenceType, true) == 0 || string.Compare(w.Name, persistenceType, true) == 0).FirstOrDefault();

            if (persistenceManifestDefinition != null)
            {
                return persistenceManifestDefinition.Name.Split('.').FirstOrDefault();
            }

            return null;
        }
    }
}


