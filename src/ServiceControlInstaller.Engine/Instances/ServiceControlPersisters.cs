namespace ServiceControlInstaller.Engine.Instances
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Newtonsoft.Json;

    public static class ServiceControlPersisters
    {
        public static PersistenceManifest[] PrimaryPersistenceManifests { get; }
        public static PersistenceManifest[] AuditPersistenceManifests { get; }

        static ServiceControlPersisters()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var list = new List<PersistenceManifest>();

            var resourceNames = assembly.GetManifestResourceNames()
                .Where(name => name.EndsWith("persistence.manifest"))
                .ToArray();

            PrimaryPersistenceManifests = resourceNames
                .Where(name => name.StartsWith(@"Particular.ServiceControl\Persisters"))
                .Select(name => Load(assembly, name))
                .OrderBy(m => m.Name)
                .ToArray();

            AuditPersistenceManifests = resourceNames
                .Where(name => name.StartsWith(@"Particular.ServiceControl.Audit\Persisters"))
                .Select(name => Load(assembly, name))
                .OrderBy(m => m.Name)
                .ToArray();
        }

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
