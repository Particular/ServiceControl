namespace ServiceControl.Persistence
{
    using System;
    using System.IO;
    using Microsoft.Extensions.Configuration;
    using ServiceBus.Management.Infrastructure.Settings;

    static class PersistenceFactory
    {
        public static IPersistence Create(IConfiguration configuration, Settings settings)
        {
            var persistenceConfiguration = CreatePersistenceConfiguration(settings);
            var section = configuration.GetSection(PrimaryOptions.SectionName);
            var persistence = persistenceConfiguration.Create(section);
            return persistence;
        }

        static IPersistenceConfiguration CreatePersistenceConfiguration(Settings settings)
        {
            try
            {
                var persistenceManifest = PersistenceManifestLibrary.Find(settings.ServiceControl.PersistenceType);
                var assemblyPath = Path.Combine(persistenceManifest.Location, $"{persistenceManifest.AssemblyName}.dll");
                var loadContext = settings.ServiceControl.AssemblyLoadContextResolver(assemblyPath);
                var customizationType = Type.GetType(persistenceManifest.TypeName, loadContext.LoadFromAssemblyName, null, true);

                return (IPersistenceConfiguration)Activator.CreateInstance(customizationType);
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load persistence customization type {settings.ServiceControl.PersistenceType}.", e);
            }
        }
    }
}