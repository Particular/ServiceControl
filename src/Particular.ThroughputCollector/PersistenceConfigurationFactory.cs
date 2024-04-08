namespace Particular.ThroughputCollector;

using System;
using Particular.ThroughputCollector.Configuration;
using Particular.ThroughputCollector.Persistence;
using ServiceControl.Configuration;

static class PersistenceConfigurationFactory
{
    public static IPersistenceConfiguration LoadPersistenceConfiguration(string persistenceType)
    {
        var manifest = PersistenceManifestLibrary.Find(persistenceType) ?? throw new InvalidOperationException($"No manifest found for {persistenceType} persistenceType");
        var type = Type.GetType(manifest.TypeName) ?? throw new InvalidOperationException($"Could not load type '{manifest.TypeName}' for requested persistence type '{persistenceType}'");

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