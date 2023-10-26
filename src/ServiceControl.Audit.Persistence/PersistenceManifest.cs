﻿namespace ServiceControl.Audit.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using NServiceBus.Logging;
    using ServiceControl.Persistence;

    public class PersistenceManifest
    {
        public string Version { get; set; }

        public string Location { get; set; }

        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string Description { get; set; }

        public string TypeName { get; set; }

        public bool IsSupported { get; set; } = true;

        public string[] Aliases { get; set; } = Array.Empty<string>();

        internal bool IsMatch(string persistenceType) =>
            string.Compare(TypeName, persistenceType, false) == 0 // Type names are case-sensitive
            || string.Compare(Name, persistenceType, true) == 0
            || AliasesContain(persistenceType);

        bool AliasesContain(string transportType)
        {
            if (Aliases == null)
            {
                return false;
            }

            return Aliases.Contains(transportType);
        }
    }

    public static class PersistenceManifestLibrary
    {
        public static List<PersistenceManifest> PersistenceManifests { get; } = new List<PersistenceManifest>();

        static PersistenceManifestLibrary()
        {
            var assemblyDirectory = GetAssemblyDirectory();

            try
            {
                foreach (var manifestFile in Directory.EnumerateFiles(assemblyDirectory, "persistence.manifest", SearchOption.AllDirectories))
                {
                    var manifest = JsonSerializer.Deserialize<PersistenceManifest>(File.ReadAllText(manifestFile));
                    manifest.Location = Path.GetDirectoryName(manifestFile);

                    PersistenceManifests.Add(manifest);
                }
            }
            catch (Exception ex)
            {
                logger.Warn($"Failed to load persistence manifests from {assemblyDirectory}", ex);
            }

            try
            {
                foreach (var manifestFile in DevelopmentPersistenceLocations.ManifestFiles)
                {
                    var manifest = JsonSerializer.Deserialize<PersistenceManifest>(File.ReadAllText(manifestFile));
                    manifest.Location = Path.GetDirectoryName(manifestFile);

                    PersistenceManifests.Add(manifest);
                }
            }
            catch (Exception ex)
            {
                logger.Warn($"Failed to load persistence manifests from development locations", ex);
            }

            PersistenceManifests.ForEach(m => logger.Info($"Found persistence manifest for {m.DisplayName}"));
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

            var persistenceManifestDefinition = PersistenceManifests.FirstOrDefault(w => w.IsMatch(persistenceType));

            return persistenceManifestDefinition?.TypeName ?? persistenceType;
        }

        public static string GetPersistenceFolder(string persistenceType)
        {
            if (persistenceType == null)
            {
                throw new Exception("No persistenceType has been configured. Either provide a Type or Name in the PersistenceType setting.");
            }

            var persistenceManifestDefinition = PersistenceManifests.FirstOrDefault(w => w.IsMatch(persistenceType));

            return persistenceManifestDefinition?.Location;
        }

        static readonly ILog logger = LogManager.GetLogger(typeof(PersistenceManifestLibrary));
    }
}


