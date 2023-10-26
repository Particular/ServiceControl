namespace ServiceControlInstaller.Engine.Instances
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Newtonsoft.Json;

    public static class ServiceControlPersisters
    {
        static readonly PersistenceManifest[] primaryPersistenceManifests;
        static readonly PersistenceManifest[] auditPersistenceManifests;

        static ServiceControlPersisters()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var list = new List<PersistenceManifest>();

            var resourceNames = assembly.GetManifestResourceNames()
                .Where(name => name.EndsWith("persistence.manifest"))
                .ToArray();

            primaryPersistenceManifests = resourceNames
                .Where(name => name.StartsWith(@"Particular.ServiceControl\Persisters"))
                .Select(name => Load(assembly, name))
                .OrderBy(m => m.Name)
                .ToArray();

            auditPersistenceManifests = resourceNames
                .Where(name => name.StartsWith(@"Particular.ServiceControl.Audit\Persisters"))
                .Select(name => Load(assembly, name))
                .OrderBy(m => m.Name)
                .ToArray();
        }

        public static PersistenceManifest GetPrimaryPersistence(string name) => primaryPersistenceManifests.Single(m => m.IsMatch(name));
        public static PersistenceManifest GetAuditPersistence(string name) => auditPersistenceManifests.Single(m => m.IsMatch(name));

        public static PersistenceManifest[] GetAllPrimaryManifests() => primaryPersistenceManifests;
        public static PersistenceManifest[] GetAllAuditManifests() => auditPersistenceManifests;

        static PersistenceManifest Load(Assembly assembly, string resourceName)
        {
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            using (var reader = new StreamReader(stream))
            {
                var manifestContent = reader.ReadToEnd();
                var manifest = JsonConvert.DeserializeObject<PersistenceManifest>(manifestContent);

                return manifest;
            }
        }
    }
}
