namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing;
    using Raven.Client;
    using ServiceControl.Audit.Persistence.RavenDb.Indexes;

    class RavenDbFailedAuditStorage : IFailedAuditStorage
    {
        readonly IDocumentStore store;

        public RavenDbFailedAuditStorage(IDocumentStore store) => this.store = store;

        public async Task Store(dynamic failure)
        {
            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(failure);

                await session.SaveChangesAsync();
            }
        }

        public async Task SaveFailedAuditImport(FailedAuditImport message)
        {
            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(message);
                await session.SaveChangesAsync();
            }
        }

        public async Task ProcessFailedMessages(
            Func<FailedTransportMessage, Func<CancellationToken, Task>, CancellationToken, Task> onMessage,
            CancellationToken cancellationToken)
        {
            using (var session = store.OpenAsyncSession())
            {
                var query = session.Query<FailedAuditImport, FailedAuditImportIndex>();
                using (var stream = await session.Advanced.StreamAsync(query, cancellationToken)
                           )
                {
                    while (!cancellationToken.IsCancellationRequested &&
                           await stream.MoveNextAsync())
                    {
                        FailedTransportMessage transportMessage = stream.Current.Document.Message;

                        await onMessage(transportMessage, (token) => store.AsyncDatabaseCommands.DeleteAsync(stream.Current.Key, null, token), cancellationToken);
                    }
                }
            }
        }

        public Task<int> GetFailedAuditsCount()
        {
            using (var session = store.OpenAsyncSession())
            {
                return session.Query<FailedAuditImport, FailedAuditImportIndex>().CountAsync();
            }
        }
    }
}