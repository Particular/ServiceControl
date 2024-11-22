namespace ServiceControlInstaller.Engine.Instances
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json;

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

        public static PersistenceManifest GetPrimaryPersistence(string name) => GetPersistenceManifest(primaryPersistenceManifests, name);
        public static PersistenceManifest GetAuditPersistence(string name) => GetPersistenceManifest(auditPersistenceManifests, name);

        static PersistenceManifest GetPersistenceManifest(PersistenceManifest[] manifestCollection, string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                // Must always remain RavenDB35 so that SCMU understands that an instance with no configured value is an old Raven 3.5 instance
                name = "RavenDB35";
            }

            return manifestCollection.FirstOrDefault(m => m.IsMatch(name)) ?? new PersistenceManifest
            {
                Name = name,
                DisplayName = "Unknown Persistence: " + name,
                IsSupported = false,
                Description = "This persistence is unknown. It may be from a future version of ServiceControl."
            };
        }

        public static PersistenceManifest[] GetAllPrimaryManifests() => primaryPersistenceManifests;
        public static PersistenceManifest[] GetAllAuditManifests() => auditPersistenceManifests;

        static PersistenceManifest Load(Assembly assembly, string resourceName)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            var manifest = JsonSerializer.Deserialize<PersistenceManifest>(stream);
            return manifest;
        }
    }
}