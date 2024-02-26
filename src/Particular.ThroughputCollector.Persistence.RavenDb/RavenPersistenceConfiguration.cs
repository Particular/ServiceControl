namespace Particular.ThroughputCollector.Persistence.RavenDb;

using System.Collections.Generic;

public class RavenPersistenceConfiguration : IPersistenceConfiguration
{
    public string Name => "RavenDB";

    public IEnumerable<string> ConfigurationKeys => new string[0];

    public IPersistence Create(PersistenceSettings settings) => new RavenPersistence(new DatabaseConfiguration("", 1, true, TimeSpan.Zero, 1, 1, new ServerConfiguration(""))); //TODO this needs work
}

