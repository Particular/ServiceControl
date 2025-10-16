namespace ServiceControl.Audit.Persistence
{
    using System;
    using System.IO;
    using Configuration;
    using Microsoft.Extensions.Configuration;
    using ServiceControl.Audit.Infrastructure.Settings;

    static class PersistenceConfigurationFactory
    {
        public static IPersistenceConfiguration LoadPersistenceConfiguration(Settings settings)
        {
            try
            {
                var persistenceManifest = PersistenceManifestLibrary.Find(settings.PersistenceType);
                var assemblyPath = Path.Combine(persistenceManifest.Location, $"{persistenceManifest.AssemblyName}.dll");
                var loadContext = settings.AssemblyLoadContextResolver(assemblyPath);
                var customizationType = Type.GetType(persistenceManifest.TypeName, loadContext.LoadFromAssemblyName, null, true);

                // TODO: Why not just a big switch and have all types accessible by referencing all persisters?
                return (IPersistenceConfiguration)Activator.CreateInstance(customizationType);
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load persistence customization type {settings.PersistenceType}.", e);
            }
        }

        public static PersistenceSettings BuildPersistenceSettings(
            Settings settings,
            IConfiguration configuration      // TODO: Remove this dependency
            )
        {
            var persistenceSettings = new PersistenceSettings(
                auditRetentionPeriod: settings.AuditRetentionPeriod,
                enableFullTextSearchOnBodies: settings.EnableFullTextSearchOnBodies,
                maxBodySizeToStore: settings.MaxBodySizeToStore
                );
            return persistenceSettings;
        }
    }
}