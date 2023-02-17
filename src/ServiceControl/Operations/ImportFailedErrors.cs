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
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Transports;

    class ImportFailedErrors
    {
        public ImportFailedErrors(
            IDocumentStore store,
            ErrorIngestor errorIngestor,
            Settings settings,
            TransportCustomization transportCustomization,
            TransportSettings transportSettings)
        {
            this.store = store;
            this.errorIngestor = errorIngestor;
            this.settings = settings;
            this.transportCustomization = transportCustomization;
            this.transportSettings = transportSettings;
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            var dispatcher = await transportCustomization.InitializeDispatcher("ImportFailedErrors", transportSettings).ConfigureAwait(false);

            if (settings.ForwardErrorMessages)
            {
                await errorIngestor.VerifyCanReachForwardingAddress(dispatcher).ConfigureAwait(false);
            }

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

                            await errorIngestor.Ingest(new List<MessageContext> { messageContext }, dispatcher).ConfigureAwait(false);

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

        readonly IDocumentStore store;
        readonly ErrorIngestor errorIngestor;
        readonly Settings settings;
        readonly TransportCustomization transportCustomization;
        readonly TransportSettings transportSettings;
        static readonly TransportTransaction EmptyTransaction = new TransportTransaction();
        static readonly CancellationTokenSource EmptyTokenSource = new CancellationTokenSource();
        static readonly ContextBag EmptyContextBag = new ContextBag();
        static readonly ILog Logger = LogManager.GetLogger(typeof(ImportFailedErrors));
    }
}