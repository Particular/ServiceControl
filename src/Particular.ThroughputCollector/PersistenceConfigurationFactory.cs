namespace Particular.ThroughputCollector;

using System;
using Particular.ThroughputCollector.Configuration;
using Particular.ThroughputCollector.Persistence;
using ServiceControl.Configuration;

static class PersistenceConfigurationFactory
{
    public static IPersistenceConfiguration LoadPersistenceConfiguration(string persistenceType)
    {
        var foundPersistenceType = PersistenceManifestLibrary.Find(persistenceType);
        var customizationType = Type.GetType(foundPersistenceType);

        if (customizationType != null &&
            Activator.CreateInstance(customizationType) is IPersistenceConfiguration config)
        {
            return config;
        }

        var e = new InvalidOperationException("Could not load configured persistence type");
        e.Data.Add(nameof(persistenceType), persistenceType);
        e.Data.Add(nameof(foundPersistenceType), foundPersistenceType);

        throw e;
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