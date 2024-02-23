namespace Particular.ThroughputCollector;

using System;
using Particular.ThroughputCollector.Configuration;
using Particular.ThroughputCollector.Persistence;
using ServiceControl.Configuration;

static class PersistenceConfigurationFactory
{
    public static IPersistenceConfiguration LoadPersistenceConfiguration(string persistenceType)
    {
        try
        {
            var foundPersistenceType = PersistenceManifestLibrary.Find(persistenceType);
            var customizationType = Type.GetType(foundPersistenceType, true);

            var config = (IPersistenceConfiguration?)Activator.CreateInstance(customizationType!);

            if (config == null)
            {
                var e = new InvalidOperationException("Could not instantiate persistence configuration");
                e.Data.Add("foundPersistenceType", foundPersistenceType);
                throw new Exception();
            }

            return config;
        }
        catch (Exception e)
        {
            throw new Exception($"Could not load persistence customization type {persistenceType}.", e);
        }
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