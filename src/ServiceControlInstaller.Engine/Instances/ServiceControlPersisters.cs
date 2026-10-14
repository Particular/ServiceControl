namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json;

    public static class ServiceControlPersisters
    {
        const string RavenDB35 = "RavenDB35";

        static readonly PersistenceManifest[] primaryPersistenceManifests;
        static readonly PersistenceManifest[] auditPersistenceManifests;
        static readonly PersistenceManifest[] supportedPrimaryPersistenceManifests;
        static readonly PersistenceManifest[] supportedAuditPersistenceManifests;

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

            // Filtered by environment variable in SCMU, needs to be available all the time for PowerShell.
            // RavenDB35 is never a user choice - it's a detection-only entry for unconfigured legacy instances.
            var includePersistencePicker = IncludePersistencePicker();
            supportedPrimaryPersistenceManifests = primaryPersistenceManifests
                .Where(m => m.Name != RavenDB35 && (includePersistencePicker || m.Name == StorageEngineNames.RavenDB))
                .ToArray();
            supportedAuditPersistenceManifests = auditPersistenceManifests
                .Where(m => m.Name != RavenDB35 && (includePersistencePicker || m.Name == StorageEngineNames.RavenDB))
                .ToArray();
        }

        public static PersistenceManifest GetPrimaryPersistence(string name) => GetPersistenceManifest(primaryPersistenceManifests, name);
        public static PersistenceManifest GetAuditPersistence(string name) => GetPersistenceManifest(auditPersistenceManifests, name);

        public static PersistenceManifest[] GetSupportedPrimaryManifests() => supportedPrimaryPersistenceManifests;
        public static PersistenceManifest[] GetSupportedAuditManifests() => supportedAuditPersistenceManifests;

        static PersistenceManifest GetPersistenceManifest(PersistenceManifest[] manifestCollection, string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                // Must always remain RavenDB35 so that SCMU understands that an instance with no configured value is an old Raven 3.5 instance
                name = RavenDB35;
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

        static bool IncludePersistencePicker()
        {
            try
            {
                var environmentValue = Environment.GetEnvironmentVariable("ServiceControl_IncludePersistencePicker");

                if (environmentValue != null)
                {
                    environmentValue = Environment.ExpandEnvironmentVariables(environmentValue);
                    if (bool.TryParse(environmentValue, out var enabled))
                    {
                        return enabled;
                    }
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
