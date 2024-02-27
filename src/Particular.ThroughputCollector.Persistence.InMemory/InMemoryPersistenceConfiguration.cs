namespace Particular.ThroughputCollector.Persistence.InMemory;

using System.Collections.Generic;

public class InMemoryPersistenceConfiguration : IPersistenceConfiguration
{
    public string Name => "InMemory";

    public IEnumerable<string> ConfigurationKeys => System.Array.Empty<string>();

    public IPersistence Create(PersistenceSettings settings) => new InMemoryPersistence();
}

