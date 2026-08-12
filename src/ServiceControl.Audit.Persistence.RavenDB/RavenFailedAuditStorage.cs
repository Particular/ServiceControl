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

    class RavenFailedAuditStorage(IRavenSessionProvider sessionProvider) : IFailedAuditStorage
    {
        public async Task SaveFailedAuditImport(FailedAuditImport message, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            await session.StoreAsync(message, token: cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        public async Task ProcessFailedMessages(
            Func<FailedTransportMessage, Func<CancellationToken, Task>, CancellationToken, Task> onMessage,
            CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
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

        public async Task<int> GetFailedAuditsCount(CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            return await session.Query<FailedAuditImport, FailedAuditImportIndex>()
                .CountAsync(cancellationToken);
        }
    }
}