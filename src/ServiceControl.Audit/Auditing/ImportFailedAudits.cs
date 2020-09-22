namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Raw;
    using NServiceBus.Transport;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Indexes;
    using Raven.Client.Documents.Operations;

    class ImportFailedAudits
    {
        public ImportFailedAudits(IDocumentStore store, AuditIngestor auditIngestor, RawEndpointFactory rawEndpointFactory)
        {
            this.store = store;
            this.auditIngestor = auditIngestor;
            this.rawEndpointFactory = rawEndpointFactory;
        }

        public Task Run(CancellationToken tokenSource)
        {
            source = tokenSource;
            return Task.Run(() => Run<FailedAuditImport, FailedAuditImportIndex>(source));
        }

        async Task Run<T, I>(CancellationToken token) where I : AbstractIndexCreationTask, new()
        {
            var config = rawEndpointFactory.CreateFailedAuditsSender("ImportFailedAudits");
            var endpoint = await RawEndpoint.Start(config).ConfigureAwait(false);

            await auditIngestor.Initialize(endpoint).ConfigureAwait(false);

            var succeeded = 0;
            var failed = 0;
            using (var session = store.OpenAsyncSession())
            {
                var query = session.Query<T, I>();
                var ie = await session.Advanced.StreamAsync(query, token)
                    .ConfigureAwait(false);
                while (!token.IsCancellationRequested && await ie.MoveNextAsync().ConfigureAwait(false))
                {
                    FailedTransportMessage dto = ((dynamic)ie.Current.Document).Message;
                    try
                    {
                        var messageContext = new MessageContext(dto.Id, dto.Headers, dto.Body, EmptyTransaction, EmptyTokenSource, EmptyContextBag);
                        var taskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        messageContext.SetTaskCompletionSource(taskCompletionSource);

                        await auditIngestor.Ingest(new List<MessageContext> { messageContext }).ConfigureAwait(false);

                        await taskCompletionSource.Task.ConfigureAwait(false);

                        // TODO: RAVEN5 - No AsyncDatabaseCommands
                        //await store.AsyncDatabaseCommands.DeleteAsync(ie.Current.Key, null, token)
                        //    .ConfigureAwait(false);
                        // Something like this (BELOW)
                        //await store.Operations.SendAsync(new DeleteByQueryOperation<FailedAuditImport>(typeof(I).Name, import => import.Id == ie.Current.Id))
                        //    .ConfigureAwait(false);
                        succeeded++;
                        if (Logger.IsDebugEnabled)
                        {
                            Logger.Debug($"Successfully re-imported failed audit message {dto.Id}.");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // no-op
                    }
                    catch (Exception e)
                    {
                        Logger.Error($"Error while attempting to re-import failed audit message {dto.Id}.", e);
                        failed++;
                    }
                }
            }

            Logger.Info($"Done re-importing failed audits. Successfully re-imported {succeeded} messages. Failed re-importing {failed} messages.");

            if (failed > 0)
            {
                Logger.Warn($"{failed} messages could not be re-imported. This could indicate a problem with the data. Contact Particular support if you need help with recovering the messages.");
            }
        }

        IDocumentStore store;
        AuditIngestor auditIngestor;
        RawEndpointFactory rawEndpointFactory;
        CancellationToken source;

        static TransportTransaction EmptyTransaction = new TransportTransaction();
        static CancellationTokenSource EmptyTokenSource = new CancellationTokenSource();
        static ContextBag EmptyContextBag = new ContextBag();
        static readonly ILog Logger = LogManager.GetLogger(typeof(ImportFailedAudits));
    }
}