namespace ServiceControl.Audit.AcceptanceTests.TestSupport;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IAcceptanceTestStorageConfiguration
{
    string PersistenceType { get; }

    Task<IDictionary<string, string>> CustomizeSettings(CancellationToken cancellationToken = default);

    Task Cleanup(CancellationToken cancellationToken = default);

    Task<IDisposable> UseDatabaseLifecycleLock(CancellationToken cancellationToken = default);
}
