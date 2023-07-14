namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using Raven.Client;
    using ServiceControl.Operations;

    public class FailedErrorImportDataStore : IFailedErrorImportDataStore
    {
        readonly IDocumentStore store;

        static readonly ILog Logger = LogManager.GetLogger(typeof(FailedErrorImportDataStore));

        public FailedErrorImportDataStore(IDocumentStore store)
        {
            this.store = store;
        }

        public async Task ProcessFailedErrorImports(Func<FailedTransportMessage, Task> processMessage, CancellationToken cancellationToken)
        {
            var succeeded = 0;
            var failed = 0;
            using (var session = store.OpenAsyncSession())
            {
                var query = session.Query<FailedErrorImport, FailedErrorImportIndex>();
                using (var stream = await session.Advanced.StreamAsync(query, cancellationToken)
                    .ConfigureAwait(false))
                {
                    while (!cancellationToken.IsCancellationRequested && await stream.MoveNextAsync().ConfigureAwait(false))
                    {
                        var transportMessage = stream.Current.Document.Message;
                        try
                        {
                            await processMessage(transportMessage).ConfigureAwait(false);

                            await store.AsyncDatabaseCommands.DeleteAsync(stream.Current.Key, null, cancellationToken)
                                .ConfigureAwait(false);
                            succeeded++;

                            if (Logger.IsDebugEnabled)
                            {
                                Logger.Debug($"Successfully re-imported failed error message {transportMessage.Id}.");
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            //  no-op
                        }
                        catch (Exception e)
                        {
                            Logger.Error($"Error while attempting to re-import failed error message {transportMessage.Id}.", e);
                            failed++;
                        }
                    }
                }
            }

            Logger.Info($"Done re-importing failed errors. Successfully re-imported {succeeded} messages. Failed re-importing {failed} messages.");

            if (failed > 0)
            {
                Logger.Warn($"{failed} messages could not be re-imported. This could indicate a problem with the data. Contact Particular support if you need help with recovering the messages.");
            }
        }
    }
}