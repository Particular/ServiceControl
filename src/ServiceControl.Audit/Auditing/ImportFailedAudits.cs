namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.Settings;
    using Microsoft.Extensions.Logging;
    using NServiceBus.Extensibility;
    using NServiceBus.Transport;
    using Persistence;

    public class ImportFailedAudits
    {
        public ImportFailedAudits(
            IFailedAuditStorage failedAuditStore,
            AuditIngestor auditIngestor,
            Settings settings,
            ILogger<ImportFailedAudits> logger)
        {
            this.settings = settings;
            this.failedAuditStore = failedAuditStore;
            this.auditIngestor = auditIngestor;
            this.logger = logger;
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
                            var messageContext = new MessageContext(
                                transportMessage.Id,
                                transportMessage.Headers,
                                transportMessage.Body,
                                EmptyTransaction,
                                settings.AuditQueue,
                                EmptyContextBag
                            );
                            var taskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                            messageContext.SetTaskCompletionSource(taskCompletionSource);

                            await auditIngestor.Ingest([messageContext], cancellationToken);

                            await taskCompletionSource.Task;

                            await markComplete(token);
                            succeeded++;
                            logger.LogDebug("Successfully re-imported failed audit message {transportMessageId}.", transportMessage.Id);
                        }
                        catch (OperationCanceledException e) when (token.IsCancellationRequested)
                        {
                            logger.LogInformation(e, "Cancelled");
                        }
                        catch (Exception e)
                        {
                            logger.LogError(e, "Error while attempting to re-import failed audit message {transportMessageId}.", transportMessage.Id);
                            failed++;
                        }

                    }, cancellationToken);

            logger.LogInformation("Done re-importing failed audits. Successfully re-imported {succeeded} messages. Failed re-importing {failed} messages.", succeeded, failed);

            if (failed > 0)
            {
                logger.LogWarning("{failed} messages could not be re-imported. This could indicate a problem with the data. Contact Particular support if you need help with recovering the messages.", failed);
            }
        }

        readonly IFailedAuditStorage failedAuditStore;
        readonly AuditIngestor auditIngestor;
        readonly Settings settings;
        readonly ILogger<ImportFailedAudits> logger;

        static readonly TransportTransaction EmptyTransaction = new TransportTransaction();
        static readonly ContextBag EmptyContextBag = new ContextBag();
    }
}