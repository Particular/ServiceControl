﻿namespace Particular.ThroughputCollector.Persistence;

using System.Threading;
using System.Threading.Tasks;

public interface IPersistenceInstaller
{
    Task Install(CancellationToken cancellationToken = default);
}