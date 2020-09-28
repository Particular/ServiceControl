namespace ServiceControl.Operations
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Raw;
    using NServiceBus.Transport;
    using Raven.Client.Documents;
    using Recoverability;

    class ImportFailedErrors
    {
        public ImportFailedErrors(IDocumentStore store, ErrorIngestor errorIngestor, RawEndpointFactory rawEndpointFactory)
        {
            this.store = store;
            this.errorIngestor = errorIngestor;
            this.rawEndpointFactory = rawEndpointFactory;
        }

        public async Task Run(CancellationToken token)
        {
            var config = rawEndpointFactory.CreateFailedErrorsImporter("ImportFailedErrors");
            var endpoint = await RawEndpoint.Start(config).ConfigureAwait(false);

            await errorIngestor.Initialize(endpoint).ConfigureAwait(false);

            try
            {
                var succeeded = 0;
                var failed = 0;
                using (var session = store.OpenAsyncSession())
                {
                    var query = session.Query<FailedErrorImport, FailedErrorImportIndex>();
                    var ie = await session.Advanced.StreamAsync(query, token)
                        .ConfigureAwait(false);

                    while (!token.IsCancellationRequested && await ie.MoveNextAsync().ConfigureAwait(false))
                    {
                        FailedTransportMessage dto = ((dynamic)ie.Current.Document).Message;
                        try
                        {
                            var messageContext = new MessageContext(dto.Id, dto.Headers, dto.Body, EmptyTransaction, EmptyTokenSource, EmptyContextBag);

                            await errorIngestor.Ingest(messageContext).ConfigureAwait(false);

                            //TODO:RAVEN5 missing AsyncDatabaseCommands
                            // await store.AsyncDatabaseCommands.DeleteAsync(ie.Current.Key, null, token)
                            //     .ConfigureAwait(false);
                            succeeded++;

                            if (Logger.IsDebugEnabled)
                            {
                                Logger.Debug($"Successfully re-imported failed error message {dto.Id}.");
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            //  no-op
                        }
                        catch (Exception e)
                        {
                            Logger.Error($"Error while attempting to re-import failed error message {dto.Id}.", e);
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
            finally
            {
                await endpoint.Stop().ConfigureAwait(false);
            }
        }

        IDocumentStore store;
        ErrorIngestor errorIngestor;
        RawEndpointFactory rawEndpointFactory;

        static TransportTransaction EmptyTransaction = new TransportTransaction();
        static CancellationTokenSource EmptyTokenSource = new CancellationTokenSource();
        static ContextBag EmptyContextBag = new ContextBag();
        static readonly ILog Logger = LogManager.GetLogger(typeof(ImportFailedErrors));
    }
}