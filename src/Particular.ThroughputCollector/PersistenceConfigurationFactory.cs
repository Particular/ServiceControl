namespace Particular.ThroughputCollector;

using System;
using System.Runtime.Loader;
using Particular.ThroughputCollector.Configuration;
using Particular.ThroughputCollector.Persistence;
using ServiceControl.Configuration;
using ServiceControl.Infrastructure;

static class PersistenceConfigurationFactory
{
    public static IPersistenceConfiguration LoadPersistenceConfiguration(string persistenceType, string persistenceAssembly)
    {
        var manifest = PersistenceManifestLibrary.Find(persistenceType) ?? throw new InvalidOperationException($"No manifest found for {persistenceType} persistenceType");

        var loadContext = AssemblyLoadContext.All.FirstOrDefault(lc => lc.Name is not null && lc.Name.Equals(persistenceAssembly, StringComparison.OrdinalIgnoreCase))
            ?? AssemblyLoadContext.Default;

        if (loadContext is PluginAssemblyLoadContext pluginLoadContext)
        {
            if (!pluginLoadContext.HasResolver(manifest.AssemblyPath))
            {
                pluginLoadContext.AddResolver(manifest.AssemblyPath);
            }
        }

        var type = Type.GetType(manifest.TypeName, loadContext.LoadFromAssemblyName, null, true) ?? throw new InvalidOperationException($"Could not load type '{manifest.TypeName}' for requested persistence type '{persistenceType}' from '{loadContext.Name}' load context");

        if (Activator.CreateInstance(type) is IPersistenceConfiguration config)
        {
            return config;
        }

        throw new InvalidOperationException($"{manifest.TypeName} does not implement IPersistenceConfiguration");
    }

    public static PersistenceSettings BuildPersistenceSettings(this IPersistenceConfiguration persistenceConfiguration)
    {
        var persistenceSettings = new PersistenceSettings();

        foreach (var key in persistenceConfiguration.ConfigurationKeys)
        {
            var value = SettingsReader.Read<string>(Settings.SettingsRootNamespace, key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                persistenceSettings.PersisterSpecificSettings[key] = value;
            }
        }

        return persistenceSettings;
    }
}