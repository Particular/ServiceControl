namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using MessageFailures;
    using NServiceBus.Logging;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Exceptions;
    using Raven.Client;
    using ServiceControl.Recoverability;

    class RetryBatchesDataStore : IRetryBatchesDataStore
    {
        readonly IDocumentStore documentStore;

        static ILog Log = LogManager.GetLogger(typeof(RetryBatchesDataStore));

        public RetryBatchesDataStore(IDocumentStore documentStore)
        {
            this.documentStore = documentStore;
        }

        public Task<IRetryBatchesManager> CreateRetryBatchesManager()
        {
            var session = documentStore.OpenAsyncSession();
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

                commands[commandIndex] = new PatchCommandData
                {
                    Patches = new[]
                    {
                        new PatchRequest
                        {
                            Type = PatchCommandType.Set, Name = "StageAttempts", Value = 1
                        }
                    },
                    Key = failedMessageRetry.Id
                };

                commandIndex++;
            }


            try
            {
                await documentStore.AsyncDatabaseCommands.BatchAsync(commands);
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
                await documentStore.AsyncDatabaseCommands.PatchAsync(message.Id,
                    new[]
                    {
                        new PatchRequest
                        {
                            Type = PatchCommandType.Set,
                            Name = "StageAttempts",
                            Value = message.StageAttempts + 1
                        }
                    });
            }
            catch (ConcurrencyException)
            {
                Log.DebugFormat("Ignoring concurrency exception while incrementing staging attempt count for {0}", message.FailedMessageId);
            }
        }

        public Task DeleteFailedMessageRetry(string uniqueMessageId) => documentStore.AsyncDatabaseCommands.DeleteAsync(FailedMessageRetry.MakeDocumentId(uniqueMessageId), null);
    }
}