namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using MessageFailures;
    using Microsoft.Extensions.Logging;
    using Raven.Client.Documents.Commands;
    using Raven.Client.Documents.Commands.Batches;
    using Raven.Client.Documents.Operations;
    using Raven.Client.Exceptions;
    using ServiceControl.Recoverability;

    class RetryBatchesDataStore(IRavenSessionProvider sessionProvider, IRavenDocumentStoreProvider documentStoreProvider, ExpirationManager expirationManager, ILogger<RetryBatchesDataStore> logger)
        : IRetryBatchesDataStore
    {
        public async Task<IRetryBatchesManager> CreateRetryBatchesManager()
        {
            var session = await sessionProvider.OpenSession();
            return new RetryBatchesManager(session, expirationManager);
        }

        public async Task RecordFailedStagingAttempt(IReadOnlyCollection<FailedMessage> messages,
            IReadOnlyDictionary<string, FailedMessageRetry> failedMessageRetriesById, Exception e,
            int maxStagingAttempts, string stagingId)
        {
            var commands = new ICommandData[messages.Count];
            var commandIndex = 0;
            foreach (var failedMessage in messages)
            {
                var failedMessageRetry = failedMessageRetriesById[failedMessage.Id];

                logger.LogWarning(e, "Attempt 1 of {MaxStagingAttempts} to stage a retry message {UniqueMessageId} failed", maxStagingAttempts, failedMessage.UniqueMessageId);

                commands[commandIndex] = new PatchCommandData(failedMessageRetry.Id, null, new PatchRequest
                {
                    Script = @"this.StageAttempts = args.Value",
                    Values =
                    {
                        {"Value", 1 }
                    }
                });

                commandIndex++;
            }


            try
            {
                using var session = await sessionProvider.OpenSession();
                var documentStore = await documentStoreProvider.GetDocumentStore();

                var batch = new SingleNodeBatchCommand(documentStore.Conventions, session.Advanced.Context, commands);
                await session.Advanced.RequestExecutor.ExecuteAsync(batch, session.Advanced.Context);
            }
            catch (ConcurrencyException)
            {
                logger.LogDebug(
                    "Ignoring concurrency exception while incrementing staging attempt count for {StagingId}",
                    stagingId);
            }
        }

        public async Task IncrementAttemptCounter(FailedMessageRetry message)
        {
            try
            {
                var documentStore = await documentStoreProvider.GetDocumentStore();
                await documentStore.Operations.SendAsync(new PatchOperation(message.Id, null, new PatchRequest
                {
                    Script = @"this.StageAttempts += 1"
                }));
            }
            catch (ConcurrencyException)
            {
                logger.LogDebug("Ignoring concurrency exception while incrementing staging attempt count for {MessageId}", message.FailedMessageId);
            }
        }

        public async Task DeleteFailedMessageRetry(string uniqueMessageId)
        {
            using var session = await sessionProvider.OpenSession();
            await session.Advanced.RequestExecutor.ExecuteAsync(new DeleteDocumentCommand(FailedMessageRetry.MakeDocumentId(uniqueMessageId), null), session.Advanced.Context);
        }
    }
}