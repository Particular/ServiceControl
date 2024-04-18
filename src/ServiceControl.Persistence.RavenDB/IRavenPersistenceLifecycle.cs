namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    interface IRavenPersistenceLifecycle : IDisposable
    {
        Task Initialize(CancellationToken cancellationToken = default);
    }
}