namespace ServiceControl.Auditing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using NServiceBus.Extensibility;
    using NServiceBus.Transport;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Operations;
    using ServiceControl.Persistence;

    class ImportFailedAudits(
        IFailedAuditImportDataStore failedAuditStore,
        AuditIngestor auditIngestor,
        Lazy<IMessageDispatcher> messageDispatcher,
        Settings settings,
        ILogger<ImportFailedAudits> logger)
    {
        public async Task Run(CancellationToken cancellationToken = default)
        {
            await auditIngestor.VerifyCanReachForwardingAddress(messageDispatcher.Value, cancellationToken);

            var succeeded = 0;
            var failed = 0;

            await failedAuditStore.ProcessFailedAuditImports(async (transportMessage, token) =>
            {
                try
                {
                    var messageContext = new MessageContext(
                        transportMessage.Id,
                        transportMessage.Headers,
                        transportMessage.Body,
                        EmptyTransaction,
                        settings.AuditQueue,
                        EmptyContextBag);
                    var taskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    messageContext.SetTaskCompletionSource(taskCompletionSource);

                    await auditIngestor.Ingest([messageContext], messageDispatcher.Value, token);

                    await taskCompletionSource.Task;

                    succeeded++;
                    logger.LogDebug("Successfully re-imported failed audit message {MessageId}", transportMessage.Id);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error while attempting to re-import failed audit message {MessageId}", transportMessage.Id);
                    failed++;
                }
            }, cancellationToken);

            logger.LogInformation("Done re-importing failed audits. Successfully re-imported {SuccessCount} messages. Failed re-importing {FailureCount} messages", succeeded, failed);

            if (failed > 0)
            {
                logger.LogWarning("{FailureCount} messages could not be re-imported. This could indicate a problem with the data. Contact Particular support if you need help with recovering the messages", failed);
            }
        }

        static readonly TransportTransaction EmptyTransaction = new();
        static readonly ContextBag EmptyContextBag = new();
    }
}
