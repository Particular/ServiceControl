namespace Particular.ThroughputCollector.Persistence;

using System.Collections.Generic;
using Particular.ThroughputCollector.Contracts;
using ServiceControl.Configuration;

public interface IPersistenceConfiguration
{
    string Name { get; }

    IEnumerable<string> ConfigurationKeys { get; }

    public virtual PersistenceSettings BuildPersistenceSettings()
    {
        var persistenceSettings = new PersistenceSettings();

        foreach (var key in ConfigurationKeys)
        {
            var value = SettingsReader.Read<string>(ThroughputSettings.SettingsNamespace, key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                persistenceSettings.PersisterSpecificSettings[key] = value;
            }
        }

        return persistenceSettings;
    }


    IPersistence Create(PersistenceSettings settings);
}