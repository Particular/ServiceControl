namespace Particular.ThroughputCollector.Persistence;

using System;
using System.Collections.Generic;

public class PersistenceSettings()
{
    public HashSet<string> PlatformEndpointNames { get; } = [];

    public IDictionary<string, string> PersisterSpecificSettings { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}