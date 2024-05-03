namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;

    interface IRavenDocumentStoreProvider
    {
        ValueTask<IDocumentStore> GetDocumentStore(CancellationToken cancellationToken = default);
    }
}