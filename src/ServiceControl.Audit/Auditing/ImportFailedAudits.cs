namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.Settings;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using Persistence;

    public class ImportFailedAudits
    {
        public ImportFailedAudits(
            IFailedAuditStorage failedAuditStore,
            AuditIngestor auditIngestor,
            Settings settings)
        {
            this.settings = settings;
            this.failedAuditStore = failedAuditStore;
            this.auditIngestor = auditIngestor;
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            await auditIngestor.VerifyCanReachForwardingAddress(cancellationToken);

            var succeeded = 0;
            var failed = 0;

            await failedAuditStore.ProcessFailedMessages(
                async (transportMessage, markComplete, token) =>
                    {
                        try
                        {
                            var messageContext = new MessageContext(transportMessage.Id, transportMessage.Headers, transportMessage.Body, EmptyTransaction, settings.AuditQueue, EmptyContextBag);
                            var taskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                            messageContext.SetTaskCompletionSource(taskCompletionSource);

                            await auditIngestor.Ingest([messageContext], cancellationToken);

                            await taskCompletionSource.Task;

                            await markComplete(token);
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

                    }, cancellationToken);

            Logger.Info($"Done re-importing failed audits. Successfully re-imported {succeeded} messages. Failed re-importing {failed} messages.");

            if (failed > 0)
            {
                Logger.Warn($"{failed} messages could not be re-imported. This could indicate a problem with the data. Contact Particular support if you need help with recovering the messages.");
            }
        }

        readonly IFailedAuditStorage failedAuditStore;
        readonly AuditIngestor auditIngestor;
        readonly Settings settings;

        static readonly TransportTransaction EmptyTransaction = new TransportTransaction();
        static readonly ContextBag EmptyContextBag = new ContextBag();
        static readonly ILog Logger = LogManager.GetLogger(typeof(ImportFailedAudits));
    }
}