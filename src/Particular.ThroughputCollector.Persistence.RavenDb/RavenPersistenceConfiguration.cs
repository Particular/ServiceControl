namespace Particular.ThroughputCollector.Persistence.RavenDb;

using System.Collections.Generic;

public class RavenPersistenceConfiguration : IPersistenceConfiguration
{
    const string DatabaseNameKey = "RavenDB/ThroughputDatabaseName";

    const string DefaultDatabaseName = "throughput";

    public string Name => "RavenDB";

    public IEnumerable<string> ConfigurationKeys => [DatabaseNameKey];

    public IPersistence Create(PersistenceSettings settings)
    {
        var databaseConfiguration = GetDatabaseConfiguration(settings);
        return new RavenPersistence(databaseConfiguration);
    }

    static DatabaseConfiguration GetDatabaseConfiguration(PersistenceSettings settings)
    {
        if (!settings.PersisterSpecificSettings.TryGetValue(DatabaseNameKey, out var databaseName))
        {
            databaseName = DefaultDatabaseName;
        }

        return new DatabaseConfiguration(databaseName);
    }
}