namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System.Threading.Tasks;
    using Raven.Client;

    class RavenDbFailedAuditStorage : IFailedAuditStorage
    {
        readonly IDocumentStore store;

        public RavenDbFailedAuditStorage(IDocumentStore store) => this.store = store;

        public async Task Store(dynamic failure)
        {
            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(failure)
                    .ConfigureAwait(false);

                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }
        }
    }
}