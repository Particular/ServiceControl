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
                var persitenceFolders = zipArchive.Entries.Where(e => e.FullName.StartsWith("Persisters/")).Select(e => e.FullName)
                    .Select(f => Directory.GetParent(f).Name)
                    .Distinct();

                var manifests = new List<PersistenceManifest>();

                foreach (var persitenceFolder in persitenceFolders)
                {
                    var manifestEntry = zipArchive.GetEntry($"Persisters/{persitenceFolder}/persistence.manifest");

                    using (var reader = new StreamReader(manifestEntry.Open()))
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
