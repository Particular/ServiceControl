namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using MessageFailures;
    using NServiceBus.Logging;
    using Raven.Client.Documents.Commands;
    using Raven.Client.Documents.Commands.Batches;
    using Raven.Client.Documents.Operations;
    using Raven.Client.Exceptions;
    using RavenDb5;
    using ServiceControl.Recoverability;

    class RetryBatchesDataStore : IRetryBatchesDataStore
    {
        readonly DocumentStoreProvider storeProvider;

        static readonly ILog Log = LogManager.GetLogger(typeof(RetryBatchesDataStore));

        public RetryBatchesDataStore(DocumentStoreProvider storeProvider)
        {
            this.storeProvider = storeProvider;
        }

        public Task<IRetryBatchesManager> CreateRetryBatchesManager()
        {
            var session = storeProvider.Store.OpenAsyncSession();
            var manager = new RetryBatchesManager(session);

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
                using var session = storeProvider.Store.OpenAsyncSession();

                var batch = new SingleNodeBatchCommand(storeProvider.Store.Conventions, session.Advanced.Context, commands);
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
                await storeProvider.Store.Operations.SendAsync(new PatchOperation(message.Id, null, new PatchRequest
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
            using var session = storeProvider.Store.OpenAsyncSession();
            await session.Advanced.RequestExecutor.ExecuteAsync(new DeleteDocumentCommand(FailedMessageRetry.MakeDocumentId(uniqueMessageId), null), session.Advanced.Context);
        }
    }
}