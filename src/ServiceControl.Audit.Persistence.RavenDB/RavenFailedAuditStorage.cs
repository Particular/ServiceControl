namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing;
    using Indexes;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Commands;
    using Raven.Client.Documents.Commands.Batches;

    class RavenFailedAuditStorage : IFailedAuditStorage
    {
        public RavenFailedAuditStorage(IRavenSessionProvider sessionProvider)
        {
            this.sessionProvider = sessionProvider;
        }

        public async Task Store(dynamic failure)
        {
            using (var session = sessionProvider.OpenSession())
            {
                await session.StoreAsync(failure);

                await session.SaveChangesAsync();
            }
        }

        public async Task SaveFailedAuditImport(FailedAuditImport message)
        {
            using (var session = sessionProvider.OpenSession())
            {
                await session.StoreAsync(message);
                await session.SaveChangesAsync();
            }
        }

        public async Task ProcessFailedMessages(
            Func<FailedTransportMessage, Func<CancellationToken, Task>, CancellationToken, Task> onMessage,
            CancellationToken cancellationToken)
        {
            using (var session = sessionProvider.OpenSession())
            {
                var query = session.Query<FailedAuditImport, FailedAuditImportIndex>();

                IAsyncEnumerator<StreamResult<FailedAuditImport>> stream = default;
                try
                {
                    stream = await session.Advanced.StreamAsync(query, cancellationToken);
                    while (!cancellationToken.IsCancellationRequested &&
                           await stream.MoveNextAsync())
                    {
                        FailedTransportMessage transportMessage = stream.Current.Document.Message;
                        var localSession = session;

                        await onMessage(transportMessage, (token) =>
                        {
                            localSession.Advanced.Defer(
                                new DeleteCommandData(stream.Current.Id, stream.Current.ChangeVector));
                            return Task.CompletedTask;
                        }, cancellationToken);
                    }

                    await session.SaveChangesAsync(cancellationToken);
                }
                finally
                {
                    if (stream != null)
                    {
                        await stream.DisposeAsync();
                    }
                }
            }
        }

        public async Task<int> GetFailedAuditsCount()
        {
            using (var session = sessionProvider.OpenSession())
            {
                return await session.Query<FailedAuditImport, FailedAuditImportIndex>()
                    .CountAsync();
            }
        }

        readonly IRavenSessionProvider sessionProvider;
    }
}