namespace ServiceControl.Persistence
{
    using System;
    using System.IO;
    using System.Runtime.Loader;
    using Microsoft.Extensions.Configuration;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure;

    static class PersistenceFactory
    {
        internal static Func<string, AssemblyLoadContext> AssemblyLoadContextResolver { get; set; } = static (assemblyPath) => new PluginAssemblyLoadContext(assemblyPath);

        public static IPersistence Create(IConfiguration configuration)
        {
            var persistenceConfiguration = CreatePersistenceConfiguration(configuration);
            var section = configuration.GetSection(PrimaryOptions.SectionName);
            var persistence = persistenceConfiguration.Create(section);
            return persistence;
        }

        static IPersistenceConfiguration CreatePersistenceConfiguration(IConfiguration configuration)
        {
            var persistenceType = configuration.GetValue<string>("PersistenceType");
            try
            {
                var persistenceManifest = PersistenceManifestLibrary.Find(persistenceType);
                var assemblyPath = Path.Combine(persistenceManifest.Location, $"{persistenceManifest.AssemblyName}.dll");

                var loadContext = AssemblyLoadContextResolver(assemblyPath);
                var customizationType = Type.GetType(persistenceManifest.TypeName, loadContext.LoadFromAssemblyName, null, true);

                return (IPersistenceConfiguration)Activator.CreateInstance(customizationType);
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load persistence customization type {persistenceType}.", e);
            }
        }
    }
}