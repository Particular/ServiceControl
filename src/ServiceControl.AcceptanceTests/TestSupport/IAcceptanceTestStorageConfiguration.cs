namespace ServiceControl.AcceptanceTests.TestSupport;

using System;
using System.Threading;
using System.Threading.Tasks;
using ServiceBus.Management.Infrastructure.Settings;

public interface IAcceptanceTestStorageConfiguration
{
    string PersistenceType { get; }

    Task CustomizeSettings(Settings settings, CancellationToken cancellationToken = default);

    Task Cleanup(CancellationToken cancellationToken = default);

    Task<IDisposable> UseDatabaseLifecycleLock(CancellationToken cancellationToken = default);
}
