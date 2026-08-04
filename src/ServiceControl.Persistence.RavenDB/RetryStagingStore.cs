namespace ServiceControl.Persistence.RavenDB
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MessageFailures;
    using Microsoft.Extensions.Logging;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Commands;
    using Raven.Client.Documents.Commands.Batches;
    using Raven.Client.Documents.Operations;
    using Raven.Client.Exceptions;

    class RetryStagingStore(
        IRavenSessionProvider sessionProvider,
        IRavenDocumentStoreProvider documentStoreProvider,
        ExpirationManager expirationManager,
        ILogger<RetryStagingStore> logger) : IRetryStagingStore
    {
        public async Task<Persistence.RetryBatch> GetStagingBatch()
        {
            using var session = await sessionProvider.OpenSession();

            var batch = await session.Query<RetryBatch>()
                .FirstOrDefaultAsync(b => b.Status == RetryBatchStatus.Staging);

            return batch?.ToContract();
        }

        public async Task<StagingMessage[]> GetMessagesToStage(string batchId)
        {
            using var session = await sessionProvider.OpenSession();

            var batch = await session.LoadAsync<RetryBatch>(batchId);

            if (batch == null)
            {
                return [];
            }

            var retries = await session.LoadAsync<FailedMessageRetry>(batch.FailureRetries);

            // A message claimed by an earlier batch keeps that claim, so this batch does not stage it.
            var claims = retries.Values
                .Where(retry => retry != null && retry.RetryBatchId == batchId)
                .ToArray();

            var messages = await session.LoadAsync<FailedMessage>(claims.Select(claim => claim.FailedMessageId));

            return
            [
                .. claims
                    .Select(claim => new { Claim = claim, Message = messages[claim.FailedMessageId] })
                    .Where(row => row.Message != null)
                    .Select(row => ToStagingMessage(row.Message, row.Claim.StageAttempts))
            ];
        }

        static StagingMessage ToStagingMessage(FailedMessage message, int stageAttempts)
        {
            var attempt = message.ProcessingAttempts.Last();

            return new StagingMessage(
                message.Id,
                message.UniqueMessageId,
                attempt.MessageId,
                attempt.FailureDetails.AddressOfFailingEndpoint,
                attempt.Headers,
                stageAttempts);
        }

        public async Task MarkBatchAsForwarding(string batchId, string stagingId, IReadOnlyCollection<string> stagedMessageIds)
        {
            using var session = await sessionProvider.OpenSession();

            var batch = await session.LoadAsync<RetryBatch>(batchId);

            if (batch == null)
            {
                return;
            }

            batch.Status = RetryBatchStatus.Forwarding;
            batch.StagingId = stagingId;
            batch.FailureRetries = [.. stagedMessageIds.Select(RetryDocumentDataStore.MakeFailedMessageRetriesDocumentId)];

            var retryIssued = RetryIssuedPatch();

            foreach (var uniqueMessageId in stagedMessageIds)
            {
                session.Advanced.Defer(new PatchCommandData(
                    FailedMessageIdGenerator.MakeDocumentId(uniqueMessageId),
                    null,
                    retryIssued));
            }

            await session.StoreAsync(
                new RetryBatchNowForwarding { RetryBatchId = batchId },
                RetryDocumentDataStore.NowForwardingDocumentId);

            await session.SaveChangesAsync();
        }

        public async Task DiscardBatch(string batchId)
        {
            using var session = await sessionProvider.OpenSession();

            session.Delete(batchId);

            await session.SaveChangesAsync();
        }

        public async Task<string> GetForwardingBatchId()
        {
            using var session = await sessionProvider.OpenSession();

            var nowForwarding = await session.LoadAsync<RetryBatchNowForwarding>(RetryDocumentDataStore.NowForwardingDocumentId);

            return nowForwarding?.RetryBatchId;
        }

        public async Task<Persistence.RetryBatch> GetBatch(string batchId, CancellationToken cancellationToken)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);

            var batch = await session.LoadAsync<RetryBatch>(batchId, cancellationToken);

            return batch?.ToContract();
        }

        public async Task CompleteForwarding(string batchId)
        {
            using var session = await sessionProvider.OpenSession();

            session.Delete(batchId);
            session.Delete(RetryDocumentDataStore.NowForwardingDocumentId);

            await session.SaveChangesAsync();
        }

        public async Task RecordStagingFailure(IReadOnlyCollection<string> uniqueMessageIds)
        {
            var commands = uniqueMessageIds
                .Select(ICommandData (uniqueMessageId) => new PatchCommandData(
                    RetryDocumentDataStore.MakeFailedMessageRetriesDocumentId(uniqueMessageId),
                    null,
                    new PatchRequest
                    {
                        Script = "this.StageAttempts = args.Value",
                        Values = { { "Value", 1 } }
                    }))
                .ToArray();

            try
            {
                using var session = await sessionProvider.OpenSession();
                var documentStore = await documentStoreProvider.GetDocumentStore();

                var batch = new SingleNodeBatchCommand(documentStore.Conventions, session.Advanced.Context, commands);
                await session.Advanced.RequestExecutor.ExecuteAsync(batch, session.Advanced.Context);
            }
            catch (ConcurrencyException)
            {
                logger.LogDebug("Ignoring concurrency exception while recording a staging failure");
            }
        }

        public async Task IncrementStagingAttempts(string uniqueMessageId)
        {
            try
            {
                var documentStore = await documentStoreProvider.GetDocumentStore();
                await documentStore.Operations.SendAsync(new PatchOperation(
                    RetryDocumentDataStore.MakeFailedMessageRetriesDocumentId(uniqueMessageId),
                    null,
                    new PatchRequest { Script = "this.StageAttempts += 1" }));
            }
            catch (ConcurrencyException)
            {
                logger.LogDebug("Ignoring concurrency exception while incrementing staging attempt count for {UniqueMessageId}", uniqueMessageId);
            }
        }

        public async Task RemoveFromBatch(string uniqueMessageId)
        {
            using var session = await sessionProvider.OpenSession();

            await session.Advanced.RequestExecutor.ExecuteAsync(
                new DeleteDocumentCommand(RetryDocumentDataStore.MakeFailedMessageRetriesDocumentId(uniqueMessageId), null),
                session.Advanced.Context);
        }

        PatchRequest RetryIssuedPatch()
        {
            var patch = new PatchRequest
            {
                Script = $"this.{nameof(FailedMessage.Status)} = args.Status;",
                Values = { { "Status", (int)FailedMessageStatus.RetryIssued } }
            };

            expirationManager.CancelExpiration(patch);

            return patch;
        }
    }
}
