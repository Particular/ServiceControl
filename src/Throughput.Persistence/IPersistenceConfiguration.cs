namespace Throughput.Persistence;

using System.Collections.Generic;

public interface IPersistenceConfiguration
{
    string Name { get; }

    IEnumerable<string> ConfigurationKeys { get; }

    IPersistence Create(PersistenceSettings settings);
}