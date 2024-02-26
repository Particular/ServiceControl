﻿namespace Particular.ThroughputCollector.Persistence;

using System.Collections.Generic;
using System.Threading.Tasks;

public interface IThroughputDataStore
{
    Task<IReadOnlyList<Endpoint>> GetAllEndpoints();
    Task<Endpoint> GetEndpointByNameOrQueue(string nameOrQueue);
    Task RecordEndpointThroughput(Endpoint endpoint);
}