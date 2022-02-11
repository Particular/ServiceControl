namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.SQL;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Raw;
    using NServiceBus.Transport;
    using Raven.Client;

    class ImportFailedAudits
    {
        public ImportFailedAudits(SqlStore writeStore, SqlQueryStore queryStore, AuditIngestor auditIngestor, RawEndpointFactory rawEndpointFactory)
        {
            this.writeStore = writeStore;
            this.queryStore = queryStore;
            this.auditIngestor = auditIngestor;
            this.rawEndpointFactory = rawEndpointFactory;
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            var config = rawEndpointFactory.CreateFailedAuditsSender("ImportFailedAudits");
            var endpoint = await RawEndpoint.Start(config).ConfigureAwait(false);

            await auditIngestor.Initialize(endpoint).ConfigureAwait(false);

            try
            {
                var succeeded = 0;
                var failed = 0;

                var failedImports = await queryStore.GetFailedAuditImports().ConfigureAwait(false);

                foreach (var failedImport in failedImports)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    FailedTransportMessage transportMessage = failedImport.Message;
                    try
                    {
                        var messageContext = new MessageContext(transportMessage.Id, transportMessage.Headers, transportMessage.Body, EmptyTransaction, EmptyTokenSource, EmptyContextBag);
                        var taskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        messageContext.SetTaskCompletionSource(taskCompletionSource);

                        await auditIngestor.Ingest(new List<MessageContext> { messageContext }).ConfigureAwait(false);

                        await taskCompletionSource.Task.ConfigureAwait(false);

                        await writeStore.RemoveFailedAuditImport(failedImport.Id, cancellationToken)
                            .ConfigureAwait(false);
                        succeeded++;
                        if (Logger.IsDebugEnabled)
                        {
                            Logger.Debug($"Successfully re-imported failed audit message {transportMessage.Id}.");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // no-op
                    }
                    catch (Exception e)
                    {
                        Logger.Error($"Error while attempting to re-import failed audit message {transportMessage.Id}.", e);
                        failed++;
                    }
                }

                Logger.Info($"Done re-importing failed audits. Successfully re-imported {succeeded} messages. Failed re-importing {failed} messages.");

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

        readonly SqlQueryStore queryStore;
        readonly SqlStore writeStore;
        readonly AuditIngestor auditIngestor;
        readonly RawEndpointFactory rawEndpointFactory;

        static readonly TransportTransaction EmptyTransaction = new TransportTransaction();
        static readonly CancellationTokenSource EmptyTokenSource = new CancellationTokenSource();
        static readonly ContextBag EmptyContextBag = new ContextBag();
        static readonly ILog Logger = LogManager.GetLogger(typeof(ImportFailedAudits));
    }
}