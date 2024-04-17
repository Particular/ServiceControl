namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Loader;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure;

    static class PersistenceFactory
    {
        static readonly HashSet<string> loadedComponentPersisters = [];

        public static IPersistence Create(Settings settings, bool maintenanceMode = false)
        {
            var persistenceConfiguration = CreatePersistenceConfiguration(settings);

            //HINT: This is false when executed from acceptance tests
            settings.PersisterSpecificSettings ??= persistenceConfiguration.CreateSettings(Settings.SettingsRootNamespace);
            settings.PersisterSpecificSettings.MaintenanceMode = maintenanceMode;

            var persistence = persistenceConfiguration.Create(settings.PersisterSpecificSettings);
            return persistence;
        }

        static IPersistenceConfiguration CreatePersistenceConfiguration(Settings settings)
        {
            try
            {
                var manifest = PersistenceManifestLibrary.Find(settings.PersistenceType) ?? throw new InvalidOperationException($"Cannot find persistence manifest for {settings.PersistenceType}");
                var loadContext = DetermineLoadContext(settings, manifest.AssemblyPath);
                var customizationType = Type.GetType(manifest.TypeName, loadContext.LoadFromAssemblyName, null, true);

                return (IPersistenceConfiguration)Activator.CreateInstance(customizationType);
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load persistence customization type {settings.PersistenceType}.", e);
            }
        }

        static AssemblyLoadContext DetermineLoadContext(Settings settings, string assemblyPath)
        {
            if (settings.UseDefaultAssemblyLoadContext)
            {
                return AssemblyLoadContext.Default;
            }

            var loadContext = AssemblyLoadContext.All.FirstOrDefault(alc => alc.Name is not null && alc.Name.Equals(assemblyPath, StringComparison.OrdinalIgnoreCase));

            return loadContext is null
                ? new PluginAssemblyLoadContext(assemblyPath)
                : loadContext;
        }

        public static T LoadComponentPersistence<T>(Settings settings, string componentPersistenceLocation, string componentPersistenceAssemblyPath, string componentPersistenceType)
        {
            AddDefaultComponentResolver(componentPersistenceLocation);

            var hostPersistenceManifest = PersistenceManifestLibrary.Find(settings.PersistenceType)
                ?? throw new InvalidOperationException($"No manifest found for {settings.PersistenceType} persistenceType");

            var loadContext = DetermineLoadContext(settings, hostPersistenceManifest.AssemblyPath);
            if (loadContext is PluginAssemblyLoadContext pluginLoadContext)
            {
                if (!pluginLoadContext.HasResolver(componentPersistenceAssemblyPath))
                {
                    pluginLoadContext.AddResolver(componentPersistenceAssemblyPath);
                }
            }

            var type = Type.GetType(componentPersistenceType, loadContext.LoadFromAssemblyName, null, true)
                ?? throw new InvalidOperationException($"Could not load type '{componentPersistenceType}' for requested persistence type '{hostPersistenceManifest.Name}' from '{loadContext.Name}' load context");

            if (Activator.CreateInstance(type) is not T typedComponentPersistence)
            {
                throw new InvalidOperationException($"{componentPersistenceType} is not of type {typeof(T).Name}");
            }

            return typedComponentPersistence;
        }

        static void AddDefaultComponentResolver(string componentPersistenceLocation)
        {
            if (componentPersistenceLocation == null)
            {
                throw new ArgumentNullException(nameof(componentPersistenceLocation));
            }

            if (!loadedComponentPersisters.Contains(componentPersistenceLocation))
            {
                AssemblyLoadContext.Default.Resolving += ResolveComponentPersistenceAssembly;
                loadedComponentPersisters.Add(componentPersistenceLocation);

                Assembly ResolveComponentPersistenceAssembly(AssemblyLoadContext context, AssemblyName name)
                {
                    var location = componentPersistenceLocation;
                    var path = Path.Combine(location, $"{name.Name}.dll");

                    return File.Exists(path)
                        ? context.LoadFromAssemblyPath(path)
                        : null;
                }
            }
        }
    }
}