namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using MessageFailures;
    using NServiceBus.Logging;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Commands;
    using Raven.Client.Documents.Commands.Batches;
    using Raven.Client.Documents.Operations;
    using Raven.Client.Exceptions;
    using ServiceControl.Recoverability;

    class RetryBatchesDataStore : IRetryBatchesDataStore
    {
        readonly IDocumentStore documentStore;
        readonly ExpirationManager expirationManager;

        static readonly ILog Log = LogManager.GetLogger(typeof(RetryBatchesDataStore));

        public RetryBatchesDataStore(IDocumentStore documentStore, ExpirationManager expirationManager)
        {
            this.documentStore = documentStore;
            this.expirationManager = expirationManager;
        }

        public Task<IRetryBatchesManager> CreateRetryBatchesManager()
        {
            var session = documentStore.OpenAsyncSession();
            var manager = new RetryBatchesManager(session, expirationManager);

            return Task.FromResult<IRetryBatchesManager>(manager);
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

                Log.Warn($"Attempt {1} of {maxStagingAttempts} to stage a retry message {failedMessage.UniqueMessageId} failed", e);

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
                using var session = documentStore.OpenAsyncSession();

                var batch = new SingleNodeBatchCommand(documentStore.Conventions, session.Advanced.Context, commands);
                await session.Advanced.RequestExecutor.ExecuteAsync(batch, session.Advanced.Context);
            }
            catch (ConcurrencyException)
            {
                Log.DebugFormat(
                    "Ignoring concurrency exception while incrementing staging attempt count for {0}",
                    stagingId);
            }
        }

        public async Task IncrementAttemptCounter(FailedMessageRetry message)
        {
            try
            {
                await documentStore.Operations.SendAsync(new PatchOperation(message.Id, null, new PatchRequest
                {
                    Script = @"this.StageAttempts += 1"
                }));
            }
            catch (ConcurrencyException)
            {
                Log.DebugFormat("Ignoring concurrency exception while incrementing staging attempt count for {0}", message.FailedMessageId);
            }
        }

        public async Task DeleteFailedMessageRetry(string uniqueMessageId)
        {
            using var session = documentStore.OpenAsyncSession();
            await session.Advanced.RequestExecutor.ExecuteAsync(new DeleteDocumentCommand(FailedMessageRetry.MakeDocumentId(uniqueMessageId), null), session.Advanced.Context);
        }
    }
}