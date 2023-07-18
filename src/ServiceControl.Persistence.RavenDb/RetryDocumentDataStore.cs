namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.Threading.Tasks;
    using MessageFailures;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using Raven.Json.Linq;
    using ServiceControl.Recoverability;

    public class RetryDocumentDataStore : IRetryDocumentDataStore
    {
        readonly IDocumentStore store;

        public RetryDocumentDataStore(IDocumentStore store)
        {
            this.store = store;
        }

        public async Task StageRetryByUniqueMessageIds(string batchDocumentId, string requestId, RetryType retryType, string[] messageIds,
            DateTime startTime,
            DateTime? last = null, string originator = null, string batchName = null, string classifier = null)
        {
            var commands = new ICommandData[messageIds.Length];
            
            for (var i = 0; i < messageIds.Length; i++)
            {
                commands[i] = CreateFailedMessageRetryDocument(batchDocumentId, messageIds[i]);
            }

            await store.AsyncDatabaseCommands.BatchAsync(commands)
                .ConfigureAwait(false);
        }

        static ICommandData CreateFailedMessageRetryDocument(string batchDocumentId, string messageId)
        {
            return new PatchCommandData
            {
                Patches = PatchRequestsEmpty,
                PatchesIfMissing = new[]
                {
                    new PatchRequest
                    {
                        Name = "FailedMessageId",
                        Type = PatchCommandType.Set,
                        Value = FailedMessage.MakeDocumentId(messageId)
                    },
                    new PatchRequest
                    {
                        Name = "RetryBatchId",
                        Type = PatchCommandType.Set,
                        Value = batchDocumentId
                    }
                },
                Key = FailedMessageRetry.MakeDocumentId(messageId),
                Metadata = DefaultMetadata
            };
        }

        static RavenJObject DefaultMetadata = RavenJObject.Parse($@"
                                    {{
                                        ""Raven-Entity-Name"": ""{FailedMessageRetry.CollectionName}"",
                                        ""Raven-Clr-Type"": ""{typeof(FailedMessageRetry).AssemblyQualifiedName}""
                                    }}");

        static PatchRequest[] PatchRequestsEmpty = Array.Empty<PatchRequest>();
    }
}