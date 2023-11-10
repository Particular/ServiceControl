namespace ServiceControlInstaller.Engine.Instances
{
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using Newtonsoft.Json;

    public static class ServiceControlAuditPersisters
    {
        internal static IReadOnlyList<PersistenceManifest> LoadAllManifests(string zipFilePath)
        {
            using (var zipArchive = ZipFile.OpenRead(zipFilePath))
            {
                var manifests = new List<PersistenceManifest>();

                var persistenceManifests = zipArchive.Entries.Where(e => e.Name == "persistence.manifest");

                foreach (var manifestEntry in persistenceManifests)
                {
                    using (var stream = manifestEntry.Open())
                    using (var reader = new StreamReader(stream))
                    {
                        var manifestContent = reader.ReadToEnd();
                        var manifest = JsonConvert.DeserializeObject<PersistenceManifest>(manifestContent);

                        manifests.Add(manifest);
                    }
                }

                return manifests;
            }
        }

        public static PersistenceManifest GetPersistence(string zipFilePath, string name)
        {
            var manifests = LoadAllManifests(zipFilePath);

            if (string.IsNullOrEmpty(name))
            {
                // Must always remain RavenDB35 so that SCMU understands that an instance with no configured value is an old Raven 3.5 instance
                return manifests.Single(m => m.Name == "RavenDB35");
            }

            return manifests.FirstOrDefault(m => m.Matches(name)) ?? new PersistenceManifest
            {
                Name = $"Unknown Persistence: {name}",
                Description = $"Unknown Persistence {name} may be from a future version of ServiceControl"
            };
        }
    }
}
