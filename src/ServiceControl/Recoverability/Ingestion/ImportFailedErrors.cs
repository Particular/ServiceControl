namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Raw;
    using NServiceBus.Transport;
    using Raven.Client;
    using Recoverability;
    using ServiceBus.Management.Infrastructure.Settings;

    class ImportFailedErrors
    {
        public ImportFailedErrors(IDocumentStore store, ErrorIngestor errorIngestor, RawEndpointFactory rawEndpointFactory, Settings settings)
        {
            this.store = store;
            this.errorIngestor = errorIngestor;
            this.rawEndpointFactory = rawEndpointFactory;
            this.settings = settings;
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            var config = rawEndpointFactory.CreateFailedErrorsImporter("ImportFailedErrors");
            var endpoint = await RawEndpoint.Start(config).ConfigureAwait(false);

            if (settings.ForwardErrorMessages)
            {
                await errorIngestor.VerifyCanReachForwardingAddress(endpoint).ConfigureAwait(false);
            }

            try
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
                                var messageContext = new MessageContext(transportMessage.Id, transportMessage.Headers, transportMessage.Body, EmptyTransaction, EmptyTokenSource, EmptyContextBag);
                                var taskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                                messageContext.SetTaskCompletionSource(taskCompletionSource);

                                await errorIngestor.Ingest(new List<MessageContext> { messageContext }, endpoint).ConfigureAwait(false);

                                await taskCompletionSource.Task.ConfigureAwait(false);

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
            finally
            {
                await endpoint.Stop().ConfigureAwait(false);
            }
        }

        readonly IDocumentStore store;
        readonly ErrorIngestor errorIngestor;
        readonly RawEndpointFactory rawEndpointFactory;
        readonly Settings settings;

        static readonly TransportTransaction EmptyTransaction = new TransportTransaction();
        static readonly CancellationTokenSource EmptyTokenSource = new CancellationTokenSource();
        static readonly ContextBag EmptyContextBag = new ContextBag();
        static readonly ILog Logger = LogManager.GetLogger(typeof(ImportFailedErrors));
    }
}