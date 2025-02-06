namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using Raven.Client.Documents.Commands;
    using ServiceControl.Operations;

    class FailedErrorImportDataStore(IRavenSessionProvider sessionProvider) : IFailedErrorImportDataStore
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(FailedErrorImportDataStore));

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

                        if (Logger.IsDebugEnabled)
                        {
                            Logger.Debug($"Successfully re-imported failed error message {transportMessage.Id}.");
                        }
                    }
                    catch (OperationCanceledException e) when (cancellationToken.IsCancellationRequested)
                    {
                        Logger.Info("Cancelled", e);
                    }
                    catch (Exception e)
                    {
                        Logger.Error($"Error while attempting to re-import failed error message {transportMessage.Id}.", e);
                        failed++;
                    }
                }
            }

            Logger.Info($"Done re-importing failed errors. Successfully re-imported {succeeded} messages. Failed re-importing {failed} messages.");

            if (failed > 0)
            {
                Logger.Warn($"{failed} messages could not be re-imported. This could indicate a problem with the data. Contact Particular support if you need help with recovering the messages.");
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