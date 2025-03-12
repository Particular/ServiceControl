#nullable enable

namespace ServiceControl.Persistence.RavenDB
{
    using System.Threading;
    using System.Threading.Tasks;

    interface IRavenPersistenceLifecycle
    {
        Task Initialize(CancellationToken cancellationToken = default);
        Task Stop(CancellationToken cancellationToken);
    }
}