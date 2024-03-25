namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System.Threading;
    using System.Threading.Tasks;

    interface IRavenPersistenceLifecycle : IRavenDocumentStoreProvider
    {
        Task Initialize(CancellationToken cancellationToken = default);
    }
}