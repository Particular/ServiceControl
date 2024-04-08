namespace ServiceControl.Persistence;

using System;
using System.IO;
using System.Reflection;
using Configuration;
using ServiceBus.Management.Infrastructure.Settings;

static class PersistenceFactory
{
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
            var loadContext = settings.AssemblyLoadContextResolver(manifest.AssemblyPath);
            var customizationType = Type.GetType(manifest.TypeName, loadContext.LoadFromAssemblyName, null, true);

                return (IPersistenceConfiguration)Activator.CreateInstance(customizationType);
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load persistence customization type {settings.PersistenceType}.", e);
            }
        }
    }
}