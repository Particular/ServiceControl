namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Commands;
    using Raven.Client.Documents.Commands.Batches;
    using Indexes;

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

        public async Task ProcessFailedMessages(
            Func<FailedTransportMessage, Func<CancellationToken, Task>, CancellationToken, Task> onMessage,
            CancellationToken cancellationToken)
        {
            using (var session = store.OpenAsyncSession())
            {
                var query = session.Query<FailedAuditImport, FailedAuditImportIndex>();

                IAsyncEnumerator<StreamResult<FailedAuditImport>> stream = default;
                try
                {
                    stream = await session.Advanced.StreamAsync(query, cancellationToken)
                        .ConfigureAwait(false);
                    while (!cancellationToken.IsCancellationRequested &&
                           await stream.MoveNextAsync().ConfigureAwait(false))
                    {
                        FailedTransportMessage transportMessage = stream.Current.Document.Message;
                        var localSession = session;

                        await onMessage(transportMessage, (token) =>
                        {
                            localSession.Advanced.Defer(
                                new DeleteCommandData(stream.Current.Id, stream.Current.ChangeVector));
                            return Task.CompletedTask;
                        }, cancellationToken).ConfigureAwait(false);
                    }

                    await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    if (stream != null)
                    {
                        await stream.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
        }

        public Task<int> GetFailedAuditsCount() => throw new NotImplementedException(); //TODO: implement GetFailedAuditsCout
    }
}