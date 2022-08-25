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
    using ServiceControl.Audit.Persistence;

    class ImportFailedAudits
    {
        public ImportFailedAudits(IFailedAuditStorage failedAuditStore, AuditIngestor auditIngestor, RawEndpointFactory rawEndpointFactory)
        {
            this.failedAuditStore = failedAuditStore;
            this.auditIngestor = auditIngestor;
            this.rawEndpointFactory = rawEndpointFactory;
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            var config = rawEndpointFactory.CreateFailedAuditsSender("ImportFailedAudits");
            var endpoint = await RawEndpoint.Start(config).ConfigureAwait(false);

            await auditIngestor.VerifyCanReachForwardingAddress(endpoint).ConfigureAwait(false);

            try
            {
                var succeeded = 0;
                var failed = 0;

                await failedAuditStore.ProcessFailedMessages(
                    async (transportMessage, markComplete, token) =>
                        {
                            try
                            {
                                var messageContext = new MessageContext(transportMessage.Id, transportMessage.Headers, transportMessage.Body, EmptyTransaction, EmptyTokenSource, EmptyContextBag);
                                var taskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                                messageContext.SetTaskCompletionSource(taskCompletionSource);

                                await auditIngestor.Ingest(new List<MessageContext> { messageContext }, endpoint).ConfigureAwait(false);

                                await taskCompletionSource.Task.ConfigureAwait(false);

                                await markComplete(token)
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

                        }, cancellationToken).ConfigureAwait(false);

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

        readonly IFailedAuditStorage failedAuditStore;
        readonly AuditIngestor auditIngestor;
        readonly RawEndpointFactory rawEndpointFactory;

        static readonly TransportTransaction EmptyTransaction = new TransportTransaction();
        static readonly CancellationTokenSource EmptyTokenSource = new CancellationTokenSource();
        static readonly ContextBag EmptyContextBag = new ContextBag();
        static readonly ILog Logger = LogManager.GetLogger(typeof(ImportFailedAudits));
    }
}