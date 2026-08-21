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
    using ServiceControl.Infrastructure.Ingestion;

    public class ImportFailedAudits
    {
        public ImportFailedAudits(
            IFailedAuditStorage failedAuditStore,
            AuditIngestor auditIngestor,
            Lazy<IMessageDispatcher> messageDispatcher,
            Settings settings,
            ILogger<ImportFailedAudits> logger)
        {
            this.settings = settings;
            this.failedAuditStore = failedAuditStore;
            this.auditIngestor = auditIngestor;
            this.messageDispatcher = messageDispatcher;
            this.logger = logger;
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            await auditIngestor.VerifyCanReachForwardingAddress(messageDispatcher.Value, cancellationToken);

            var succeeded = 0;
            var failed = 0;

#pragma warning disable PS0021
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

                            await auditIngestor.Ingest([messageContext], messageDispatcher.Value, cancellationToken);

                            await taskCompletionSource.Task;

                            await markComplete(token);
                            succeeded++;
                            logger.LogDebug("Successfully re-imported failed audit message {MessageId}", transportMessage.Id);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception e)
                        {
                            logger.LogError(e, "Error while attempting to re-import failed audit message {MessageId}", transportMessage.Id);
                            failed++;
                        }

                    }, cancellationToken);
#pragma warning restore PS0021

            logger.LogInformation("Done re-importing failed audits. Successfully re-imported {SuccessCount} messages. Failed re-importing {FailureCount} messages", succeeded, failed);

            if (failed > 0)
            {
                logger.LogWarning("{FailureCount} messages could not be re-imported. This could indicate a problem with the data. Contact Particular support if you need help with recovering the messages", failed);
            }
        }

        readonly IFailedAuditStorage failedAuditStore;
        readonly AuditIngestor auditIngestor;
        readonly Lazy<IMessageDispatcher> messageDispatcher;
        readonly Settings settings;
        readonly ILogger<ImportFailedAudits> logger;

        static readonly TransportTransaction EmptyTransaction = new TransportTransaction();
        static readonly ContextBag EmptyContextBag = new ContextBag();
    }
}