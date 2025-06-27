namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Raven.Client.Documents.Commands;
    using ServiceControl.Operations;

    class FailedErrorImportDataStore(IRavenSessionProvider sessionProvider, ILogger<FailedErrorImportDataStore> logger) : IFailedErrorImportDataStore
    {
        public async Task ProcessFailedErrorImports(Func<FailedTransportMessage, Task> processMessage, CancellationToken cancellationToken)
        {
            var succeeded = 0;
            var failed = 0;
            using (var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken))
            {
                var query = session.Query<FailedErrorImport, FailedErrorImportIndex>();
                await using var stream = await session.Advanced.StreamAsync(query, cancellationToken);
                while (!cancellationToken.IsCancellationRequested && await stream.MoveNextAsync())
                {
                    var transportMessage = stream.Current.Document.Message;
                    try
                    {
                        await processMessage(transportMessage);

                        await session.Advanced.RequestExecutor.ExecuteAsync(new DeleteDocumentCommand(stream.Current.Id, null), session.Advanced.Context, token: cancellationToken);

                        succeeded++;

                        logger.LogDebug("Successfully re-imported failed error message {MessageId}", transportMessage.Id);
                    }
                    catch (OperationCanceledException e) when (cancellationToken.IsCancellationRequested)
                    {
                        logger.LogInformation(e, "Cancelled");
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Error while attempting to re-import failed error message {MessageId}", transportMessage.Id);
                        failed++;
                    }
                }
            }

            logger.LogInformation("Done re-importing failed errors. Successfully re-imported {SucceededCount} messages. Failed re-importing {FailedCount} messages", succeeded, failed);

            if (failed > 0)
            {
                logger.LogWarning("{FailedCount} messages could not be re-imported. This could indicate a problem with the data. Contact Particular support if you need help with recovering the messages", failed);
            }
        }

        public async Task<bool> QueryContainsFailedImports()
        {
            using var session = await sessionProvider.OpenSession();
            var query = session.Query<FailedErrorImport, FailedErrorImportIndex>();
            await using var ie = await session.Advanced.StreamAsync(query);
            return await ie.MoveNextAsync();
        }
    }
}