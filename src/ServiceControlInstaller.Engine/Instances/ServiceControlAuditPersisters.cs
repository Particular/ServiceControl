namespace ServiceControlInstaller.Engine.Instances
{
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using Newtonsoft.Json;

    public class ServiceControlAuditPersisters
    {
        public static IReadOnlyList<PersistenceManifest> LoadAllManifests(string zipFilePath)
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
    }
}
