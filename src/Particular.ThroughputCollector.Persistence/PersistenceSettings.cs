﻿namespace Particular.ThroughputCollector.Persistence;

using System;
using System.Collections.Generic;

public class PersistenceSettings()
{
    public IDictionary<string, string> PersisterSpecificSettings { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}